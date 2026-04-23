# Running the EMS App on a Real Android Device

Follow these steps every time you want to test the APK on a physical phone.

---

## Step 1 — Find your PC's LAN IP address

Open a terminal/Command Prompt on the PC that runs the .NET backend.

**Windows (Command Prompt):**
```
ipconfig
```
Look for **IPv4 Address** under your Wi-Fi adapter. Example: `192.168.1.105`

**macOS / Linux:**
```
ifconfig | grep "inet "
```
Look for the address under `en0` (Wi-Fi). Ignore `127.0.0.1`.

---

## Step 2 — Start the .NET backend so it is visible on the network

The backend must listen on `0.0.0.0` (all interfaces), not just `localhost`.
The `Program.cs` has already been updated to do this with:
```csharp
app.Run("http://0.0.0.0:5000");
```

Start the backend:
```
cd backend/EMS.API
dotnet run
```

You should see output like:
```
Now listening on: http://0.0.0.0:5000
```

**Windows Firewall:** If the phone cannot reach the backend, Windows Firewall
may be blocking port 5000. Allow it with:
```
netsh advfirewall firewall add rule name="EMS API 5000" dir=in action=allow protocol=TCP localport=5000
```

---

## Step 3 — Build the APK with the correct backend URL

Replace `192.168.1.105` with YOUR actual LAN IP from Step 1.

```bash
cd flutter_app

# Debug APK (fastest, good for testing):
flutter build apk --debug --dart-define=API_BASE_URL=http://192.168.1.105:5000

# Release APK (for final deployment):
flutter build apk --release --dart-define=API_BASE_URL=http://192.168.1.105:5000
```

The built APK is at:
- Debug:   `flutter_app/build/app/outputs/flutter-apk/app-debug.apk`
- Release: `flutter_app/build/app/outputs/flutter-apk/app-release.apk`

---

## Step 4 — Install the APK on the phone

**Via USB (easiest):**
```
flutter install
```
Or copy the APK file to the phone and open it (enable "Install from unknown sources" in phone settings).

---

## Step 5 — Make sure both are on the same Wi-Fi

The phone and the PC running the backend MUST be connected to the **same Wi-Fi
router**. Mobile data will NOT work — the phone cannot reach a local IP on your
home/office router over the internet.

**Want it to work over mobile data or from outside your network?**
Use a tunnel like ngrok:
```
ngrok http 5000
```
Then build the APK with the ngrok URL:
```
flutter build apk --debug --dart-define=API_BASE_URL=https://xxxx.ngrok-free.app
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "Cannot connect to server" | Wrong IP or backend not running | Check IP, restart backend |
| "Connection refused" | Backend only on localhost | Ensure `app.Run("http://0.0.0.0:5000")` in Program.cs |
| App works on emulator but not phone | Using 10.0.2.2 | Pass correct `--dart-define=API_BASE_URL` |
| Works on Wi-Fi, fails on mobile data | Local IP not reachable | Use ngrok tunnel |
| Windows Firewall blocking | Port 5000 blocked | Run `netsh` command from Step 2 |
| HTTP blocked on Android 9+ | Missing network config | `network_security_config.xml` already added |
