# Deployment Runbook — Option B (GCE e2-micro, Always Free)

End-to-end steps to deploy BusinessOne to Google Cloud and keep it running. This is the
operational companion to the architecture in [docs/01 §6](../../docs/01-solution-architecture.md)
and the security/go-live checklist in [docs/12](../../docs/12-hardening-and-runbook.md).

**Topology:** one e2-micro VM runs the API + PostgreSQL + Caddy (auto-HTTPS) in Docker.
DB data lives on the VM's persistent disk; nightly `pg_dump` ships to Cloud Storage. The
React web SPA goes to Firebase Hosting (separate, free). Cost: ~$0/mo in Always-Free.

```
GCE e2-micro VM (Always Free)
  ├─ Caddy        :80/:443  → Let's Encrypt TLS, reverse proxy
  ├─ ASP.NET API  127.0.0.1:8080 (loopback only)
  └─ PostgreSQL   internal network only (no public port)
Cloud Storage   → attachments + nightly pg_dump backups
Firebase Hosting → React web SPA
```

Files referenced below all live in `infra/docker/` unless noted:
`docker-compose.prod.yml` · `Caddyfile` · `.env.example` · `../scripts/erp-backup.sh` ·
`.github/workflows/deploy.yml`.

---

## 1 · One-time GCP setup (from your machine)

Requires the `gcloud` CLI ([install](https://cloud.google.com/sdk/docs/install)).

```bash
gcloud auth login
gcloud projects create business-one-prod --name="BusinessOne"
gcloud config set project business-one-prod
# Link a billing account in the console (required even for free tier), then:
gcloud services enable compute.googleapis.com storage.googleapis.com
```

## 2 · Provision the Always-Free VM

Region **must** be `us-west1`, `us-central1`, or `us-east1` and machine type **e2-micro** to
stay in Always-Free.

```bash
gcloud compute instances create erp-vm \
  --zone=us-central1-a --machine-type=e2-micro \
  --image-family=debian-12 --image-project=debian-cloud \
  --boot-disk-size=30GB --boot-disk-type=pd-standard \
  --tags=http-server,https-server

gcloud compute firewall-rules create allow-web \
  --allow=tcp:80,tcp:443 --target-tags=http-server,https-server

gcloud compute instances describe erp-vm --zone=us-central1-a \
  --format='get(networkInterfaces[0].accessConfigs[0].natIP)'   # note the external IP
```

## 3 · DNS

Create an **A record** for your API hostname (e.g. `api.yourdomain.com`) pointing at the VM's
external IP from step 2. Caddy needs this to resolve **before** first start to obtain a cert.

## 4 · Prepare the VM

```bash
gcloud compute ssh erp-vm --zone=us-central1-a
```

Then, on the VM:

```bash
# (a) Swap — e2-micro has only 1 GB RAM; API + Postgres + Caddy will OOM without it.
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile \
  && sudo mkswap /swapfile && sudo swapon /swapfile \
  && echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# (b) Docker
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER        # then log out/in so the group applies

# (c) Source code
git clone <your-repo-url> ~/BussinessOne
cd ~/BussinessOne
```

## 5 · Secrets / environment

On the VM, create the prod env file from the template and fill in **real** values:

```bash
cp infra/docker/.env.example infra/docker/.env
nano infra/docker/.env
```

| Key | Value |
|-----|-------|
| `API_DOMAIN` | the hostname from step 3 (e.g. `api.yourdomain.com`) |
| `WEB_ORIGIN` | your web SPA origin for CORS (e.g. `https://app.yourdomain.com`) |
| `API_IMAGE` | `ghcr.io/<owner>/erp-api:latest` for the first bring-up (CI rewrites it to the git SHA after) |
| `POSTGRES_PASSWORD` | a strong random password |
| `JWT_SIGNING_KEY` | 32+ random chars — `openssl rand -base64 48` (app refuses to start if shorter) |

`.env` is gitignored — never commit it. See [docs/12 §7](../../docs/12-hardening-and-runbook.md).

## 6 · First deploy (manual, proves the stack works)

```bash
# on the VM, from ~/BussinessOne
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env up -d
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env ps

# Smoke test (loopback API port + public TLS through Caddy)
curl -fsS http://127.0.0.1:8080/health/ready          # 200 from the container
curl -fsS https://api.yourdomain.com/health/ready      # 200 through Caddy + TLS
```

`Database__AutoMigrate=true` applies EF migrations on startup, so the schema builds itself on
first run. If the image is private, `docker login ghcr.io` first (PAT with `read:packages`).

## 7 · Automated deploys (CI/CD)

`.github/workflows/deploy.yml` runs **after the CI workflow succeeds on `main`**: it builds and
pushes the API image to GHCR, then SSHes to the VM and rolls the stack.

Add these **repository secrets** (Settings → Secrets and variables → Actions):

| Secret | Value |
|--------|-------|
| `VM_HOST` | VM external IP / hostname |
| `VM_USER` | SSH user that owns `~/BussinessOne` and is in the `docker` group |
| `VM_SSH_KEY` | private SSH key whose public key is in the VM user's `authorized_keys` |
| `GHCR_TOKEN` | GitHub PAT with `read:packages` so the VM can pull (skip if the package is public) |

> **GHCR paths are lowercase.** If your GitHub owner has capital letters, replace
> `${{ github.repository_owner }}` in `deploy.yml` with the lowercase owner string.

After this, every push to `main` that passes CI auto-deploys and smoke-tests `/health/ready`.

## 8 · Database backups (RPO 24h)

The DB runs in a container on the VM (no public port). Set up nightly dumps to Cloud Storage:

```bash
# bucket + 30-day retention
gsutil mb -l us-central1 gs://business-one-backups
printf '{"rule":[{"action":{"type":"Delete"},"condition":{"age":30}}]}' > /tmp/lifecycle.json
gsutil lifecycle set /tmp/lifecycle.json gs://business-one-backups

# install the nightly cron job
sudo cp infra/scripts/erp-backup.sh /etc/cron.daily/erp-backup
sudo chmod +x /etc/cron.daily/erp-backup
```

**Rehearse the restore once before go-live** (see `erp-backup.sh` footer and
[docs/12 §4](../../docs/12-hardening-and-runbook.md)).

## 8b · File storage → Firebase Storage

Attachments (expense bills, receipts captured on the mobile app) are stored in a **Firebase Storage
bucket** — a Google Cloud Storage bucket managed by Firebase. The API uploads objects and hands
clients **short-lived signed download URLs**, so the bucket stays private. Local disk (`STORAGE_PROVIDER=Local`)
remains the default for dev/CI; production sets `STORAGE_PROVIDER=Firebase`.

**One-time setup (in the existing GCP project, e.g. `business-one-40657`):**

1. **Enable Storage.** Firebase console → **Build → Storage → Get started** (creates the default
   bucket). Copy the exact bucket name shown — newer projects are `<project>.firebasestorage.app`,
   older ones `<project>.appspot.com`. This is your `FIREBASE_BUCKET`.
2. **Create a service account** with object read/write on that bucket:
   ```bash
   gcloud config set project business-one-40657
   gcloud iam service-accounts create erp-storage --display-name="ERP file storage"
   gcloud projects add-iam-policy-binding business-one-40657 \
     --member="serviceAccount:erp-storage@business-one-40657.iam.gserviceaccount.com" \
     --role="roles/storage.objectAdmin"
   gcloud iam service-accounts keys create gcs-sa.json \
     --iam-account=erp-storage@business-one-40657.iam.gserviceaccount.com
   ```
   > The key must be a **service-account** key: the API signs download URLs offline with its private
   > key, so no extra `signBlob` IAM permission is needed and the VM never calls Google to sign.
3. **Minify the key to one line** (the API reads it from an env var, so there's no file to mount and
   the automated deploy has no file dependency):
   ```bash
   jq -c . gcs-sa.json          # copy this single-line output
   ```
4. **Set the env** in `infra/docker/.env` on the VM (see `.env.example`) — `.env` is gitignored:
   ```
   STORAGE_PROVIDER=Firebase
   FIREBASE_BUCKET=business-one-40657.firebasestorage.app     # the exact name from step 1
   FIREBASE_CREDENTIALS_JSON={"type":"service_account", ... }  # the one-line JSON from step 3
   ```
   Then bring the stack up as usual (`docker compose ... up -d`). Upload a bill from the web console
   (Expenses → Attach) and confirm the object appears in Firebase console → Storage.

> Prefer a mounted key file instead of inline JSON? Set `Storage__Firebase__CredentialsPath` to a path
> and bind-mount the key there yourself — both are supported by `GcsFileStorage`.

**Bucket CORS (optional).** The web console opens attachments via a top-level navigation
(`window.open`), which needs no CORS. Only if a browser fetches an object via `fetch()`/XHR do you
need CORS on the bucket. A ready config is in [`gcs-cors.json`](gcs-cors.json) — edit the origins, then:
```bash
gsutil cors set infra/docker/gcs-cors.json gs://business-one-40657.firebasestorage.app
```

## 9 · Web SPA → Firebase Hosting

```bash
npm i -g firebase-tools && firebase login
cd apps/web && pnpm build && firebase init hosting   # public dir: dist
firebase deploy
```

Set the API base URL to `https://api.yourdomain.com` in the web build config, and make sure
that origin matches `WEB_ORIGIN` in the VM `.env` (step 5) so CORS allows it.

---

## Routine operations

**Update (normally automatic via CI):** push to `main`. To deploy manually on the VM:
```bash
cd ~/BussinessOne && git pull --ff-only
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env pull
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env up -d
# Recreated api gets a new Docker IP; restart Caddy so it re-resolves (else 503 no-upstreams)
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env restart caddy
```

**Rollback** (docs/12 §5) — pin the previous image tag and bring it back up:
```bash
sed -i 's|^API_IMAGE=.*|API_IMAGE=ghcr.io/<owner>/erp-api:<previous-sha>|' infra/docker/.env
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env up -d
# If a migration is incompatible, restore the pre-deploy pg_dump (migrations are forward-only).
```

**Logs / status:**
```bash
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env logs -f api
docker compose -f infra/docker/docker-compose.prod.yml --env-file infra/docker/.env ps
```

**Go-live checklist:** complete [docs/12 §7](../../docs/12-hardening-and-runbook.md) before
the pilot — prod signing key set, TLS+HSTS on, backup cron live + restore rehearsed, CORS
locked to prod origins, `/health/ready` wired to uptime monitoring.

## Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| Caddy can't get a cert | DNS A record not pointing at the VM yet, or ports 80/443 not open (firewall rule in step 2). |
| API restarts / OOM | Swap not added (step 4a); confirm with `free -h`. |
| `denied` pulling image | VM not logged into GHCR, or `API_IMAGE` owner not lowercase. |
| API won't start, key error | `JWT_SIGNING_KEY` shorter than 32 chars (docs/12 §1). |
| 503 "no upstreams available" | api container was recreated with a new Docker IP; `docker compose … restart caddy` so Caddy re-resolves it. |
| DB connection refused | Run all compose commands with the same `--env-file`; check `docker ... ps` shows `erp-db` healthy. |
