// Device services: push-notification registration (FCM via Expo) and camera capture.

import * as Device from "expo-device";
import * as Notifications from "expo-notifications";

/** Registers for push and returns the device (FCM) token to send to the backend, or null. */
export async function registerForPush(): Promise<string | null> {
  if (!Device.isDevice) return null;

  const { status: existing } = await Notifications.getPermissionsAsync();
  let status = existing;
  if (status !== "granted") {
    status = (await Notifications.requestPermissionsAsync()).status;
  }
  if (status !== "granted") return null;

  const token = await Notifications.getDevicePushTokenAsync();
  return typeof token.data === "string" ? token.data : String(token.data);
}

// Camera capture is wired with expo-camera's <CameraView> in the attachment screen (Phase 8.x):
// capture → upload bytes to POST /api/v1/files → store the returned objectKey on the record.
// Left as a documented integration point to keep this scaffold focused on the offline-sync core.
