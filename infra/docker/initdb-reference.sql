CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE business_types (
        id uuid NOT NULL,
        code character varying(20) NOT NULL,
        name character varying(80) NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_business_types PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE permissions (
        id uuid NOT NULL,
        code character varying(80) NOT NULL,
        description character varying(200),
        CONSTRAINT pk_permissions PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE roles (
        id uuid NOT NULL,
        code character varying(40) NOT NULL,
        name character varying(80) NOT NULL,
        is_system boolean NOT NULL,
        CONSTRAINT pk_roles PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE role_permissions (
        role_id uuid NOT NULL,
        permission_id uuid NOT NULL,
        CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission_id),
        CONSTRAINT fk_role_permissions_permissions_permission_id FOREIGN KEY (permission_id) REFERENCES permissions (id) ON DELETE CASCADE,
        CONSTRAINT fk_role_permissions_roles_role_id FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE businesses (
        id uuid NOT NULL,
        tenant_id uuid NOT NULL,
        business_type_id uuid NOT NULL,
        name character varying(150) NOT NULL,
        gst_number character varying(20),
        address character varying(300),
        currency character varying(3) NOT NULL,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        is_deleted boolean NOT NULL,
        deleted_at timestamp with time zone,
        CONSTRAINT pk_businesses PRIMARY KEY (id),
        CONSTRAINT fk_businesses_business_types_business_type_id FOREIGN KEY (business_type_id) REFERENCES business_types (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        token_hash text NOT NULL,
        device_info character varying(200),
        expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_refresh_tokens PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE tenants (
        id uuid NOT NULL,
        name character varying(150) NOT NULL,
        timezone character varying(64) NOT NULL,
        owner_user_id uuid,
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        is_deleted boolean NOT NULL,
        deleted_at timestamp with time zone,
        CONSTRAINT pk_tenants PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        tenant_id uuid,
        full_name character varying(150) NOT NULL,
        mobile character varying(20) NOT NULL,
        email character varying(150),
        password_hash text NOT NULL,
        is_super_admin boolean NOT NULL,
        is_active boolean NOT NULL,
        last_login_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        is_deleted boolean NOT NULL,
        deleted_at timestamp with time zone,
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT fk_users_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE TABLE user_businesses (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        business_id uuid NOT NULL,
        role_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        is_deleted boolean NOT NULL,
        deleted_at timestamp with time zone,
        CONSTRAINT pk_user_businesses PRIMARY KEY (id),
        CONSTRAINT fk_user_businesses_businesses_business_id FOREIGN KEY (business_id) REFERENCES businesses (id) ON DELETE CASCADE,
        CONSTRAINT fk_user_businesses_roles_role_id FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT,
        CONSTRAINT fk_user_businesses_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_business_types_code ON business_types (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_businesses_business_type_id ON businesses (business_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_businesses_tenant_id_name ON businesses (tenant_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_permissions_code ON permissions (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_role_permissions_permission_id ON role_permissions (permission_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_roles_code ON roles (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_tenants_owner_user_id ON tenants (owner_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_user_businesses_business_id ON user_businesses (business_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_user_businesses_role_id ON user_businesses (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_user_businesses_user_id_business_id ON user_businesses (user_id, business_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE INDEX ix_users_email ON users (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    CREATE UNIQUE INDEX ix_users_tenant_id_mobile ON users (tenant_id, mobile);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    ALTER TABLE businesses ADD CONSTRAINT fk_businesses_tenants_tenant_id FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    ALTER TABLE refresh_tokens ADD CONSTRAINT fk_refresh_tokens_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    ALTER TABLE tenants ADD CONSTRAINT fk_tenants_users_owner_user_id FOREIGN KEY (owner_user_id) REFERENCES users (id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20260623100000_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20260623100000_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

