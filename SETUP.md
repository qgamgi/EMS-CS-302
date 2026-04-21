# EMS Dispatch System — Step-by-Step Setup Guide

This guide walks you through running the full system end-to-end:
**ML Service (FastAPI)** → **Backend API (ASP.NET Core + SignalR)** → **Database (MongoDB)** → **Mobile App (Flutter)**

---

## Prerequisites

Install the following tools before starting.

| Tool | Version | Download |
|---|---|---|
| Docker Desktop | 24+ | https://www.docker.com/products/docker-desktop |
| Docker Compose | 2.20+ (bundled with Docker Desktop) | — |
| Python | 3.11+ | https://www.python.org/downloads |
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Flutter SDK | 3.22+ | https://docs.flutter.dev/get-started/install |
| Git | any | https://git-scm.com |

> **Note:** Docker is required to run MongoDB and wire all services together. Python and .NET are only needed if you want to run services outside Docker.

---

## Step 1 — Clone the Repository

```bash
git clone https://github.com/qgamgi/EMS-CS-302.git
cd EMS-CS-302
```

---

## Step 2 — Train the ML Model

The ML service needs a trained model file before it can start. Run the training script once.

```bash
# Install Python dependencies
pip install scikit-learn pandas numpy matplotlib

# Create required folders
mkdir -p models/analysis

# Run the training script (requires datasets/patient/marikina_patients_ml.csv)
python train_model.py
```

After this finishes you should see:

```
models/
  hospital_prediction_model.pkl
  le_severity.pkl
  le_condition.pkl
  analysis/
    confusion_matrix.png
```

> If you do not have the dataset yet, contact your team to obtain `datasets/patient/marikina_patients_ml.csv` and place it in that path before running the command above.

---

## Step 3 — Configure Environment Variables

Copy the example environment file and fill in your values.

```bash
cp .env.example .env
```

Open `.env` and edit:

```env
# Strong password for the MongoDB admin user
MONGO_PASSWORD=changeme

# JWT signing secret — must be at least 32 characters
JWT_SECRET=your-super-secret-key-at-least-32-characters-long

# Password for Mongo Express (dev browser UI)
MONGO_EXPRESS_PASSWORD=admin123
```

> Do **not** commit the `.env` file to version control. It is already in `.gitignore`.

---

## Step 4 — Run All Backend Services with Docker

A single command starts MongoDB, the ML service, and the ASP.NET Core API.

### Production mode (3 services only)

```bash
docker compose up --build
```

### Development mode (adds Mongo Express browser UI on port 8081)

```bash
docker compose --profile dev up --build
```

The first run will take several minutes to pull images and build the containers. Subsequent starts are fast.

### Verify all services are healthy

```bash
docker compose ps
```

You should see all services in `healthy` or `running` state:

| Service | Port | URL |
|---|---|---|
| MongoDB | 27017 | (internal only) |
| ML Service (FastAPI) | 8000 | http://localhost:8000/docs |
| Backend API (ASP.NET) | 5000 | http://localhost:5000/swagger |
| Mongo Express (dev only) | 8081 | http://localhost:8081 |

### Quick health checks

```bash
# ML Service
curl http://localhost:8000/health

# Backend API
curl http://localhost:5000/health
```

Both should return `{"status":"ok",...}`.

---

## Step 5 — Seed the Database

The MongoDB init scripts run automatically on first startup (Docker mounts `scripts/` into the container). Verify seeding worked by checking Mongo Express at http://localhost:8081 or by running:

```bash
docker exec -it ems-mongodb mongosh \
  -u admin -p changeme --authenticationDatabase admin \
  ems_dispatch \
  --eval "db.ems_bases.countDocuments()"
```

You should see `6` (one document per Marikina EMS base).

---

## Step 6 — Create the First Admin User

The `/api/auth/register` endpoint is open only for the first user or when called with an existing Admin JWT. Register your admin account:

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "Admin@1234",
    "fullName": "System Administrator",
    "role": "Admin"
  }'
```

> **Roles available:** `Admin`, `Dispatcher`, `EmsOperator`, `Driver`

Log in to get a JWT token:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "Admin@1234"
  }'
```

Save the `token` value from the response. Use it as a `Bearer` token in the `Authorization` header for all subsequent API calls.

---

## Step 7 — Explore the APIs

### Backend REST API + Swagger UI

Open http://localhost:5000/swagger in a browser. Click **Authorize** (top right) and paste your JWT token as `Bearer <token>`.

Key endpoints:

| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/login` | Get JWT token |
| POST | `/api/auth/register` | Create a user |
| GET | `/api/dispatches` | List all dispatches |
| POST | `/api/dispatches` | Create a dispatch (calls ML service internally) |
| PATCH | `/api/dispatches/{id}/status` | Update dispatch status |
| GET | `/api/drivers` | List all drivers |
| PATCH | `/api/drivers/location` | Update driver GPS location |

### ML Service API + Swagger UI

Open http://localhost:8000/docs in a browser.

Key endpoints:

| Method | Path | Description |
|---|---|---|
| POST | `/predict` | Get hospital prediction for a patient |
| GET | `/hospitals` | List all hospitals |
| GET | `/ems-bases` | List all EMS bases |
| GET | `/meta/severities` | Valid severity values |
| GET | `/meta/conditions` | Valid medical conditions |

Example prediction call:

```bash
curl -X POST http://localhost:8000/predict \
  -H "Content-Type: application/json" \
  -d '{
    "latitude": 14.6470,
    "longitude": 121.1020,
    "severity": "high",
    "condition": "Heart attack"
  }'
```

---

## Step 8 — Run the Flutter App

### 8a — Install Flutter dependencies

```bash
cd flutter_app
flutter pub get
```

### 8b — Connect a device or emulator

**Android emulator:** Open Android Studio → Device Manager → Start an AVD.  
**Physical Android device:** Enable Developer Options → USB Debugging, then plug in.  
**iOS simulator (macOS only):** Open Xcode → Simulator.

Verify Flutter can see your device:

```bash
flutter devices
```

### 8c — Configure the API URL

The app defaults to `http://10.0.2.2:5000` (Android emulator localhost alias). For a physical device or iOS, pass your machine's local IP address:

```bash
# Find your machine's local IP (macOS/Linux)
ipconfig getifaddr en0        # macOS
hostname -I | awk '{print $1}' # Linux

# Run with your IP
flutter run --dart-define=API_BASE_URL=http://192.168.1.x:5000
```

### 8d — Run the app

```bash
flutter run
```

Log in with the admin credentials you created in Step 6. The app will route you to the correct dashboard based on your role.

---

## Step 9 — Stopping the System

```bash
# Stop all services (keeps data)
docker compose down

# Stop and delete all data (full reset)
docker compose down -v
```

---

## Troubleshooting

**ML Service fails to start**
- Ensure `models/hospital_prediction_model.pkl` exists. Re-run `python train_model.py`.
- Check that `datasets/patient/marikina_patients_ml.csv` is present.

**Backend API exits immediately**
- Check that MongoDB is healthy first: `docker compose ps`.
- Verify `JWT_SECRET` in `.env` is at least 32 characters long.

**Flutter app cannot connect**
- On Android emulator, the backend address is `http://10.0.2.2:5000`, not `localhost`.
- On a physical device, use your machine's LAN IP (e.g., `192.168.1.x`).
- Confirm the backend container is running: `docker compose ps`.

**MongoDB authentication failed**
- Make sure `.env` has the same `MONGO_PASSWORD` you used when the container was first created.
- If you changed the password, delete the volume and recreate: `docker compose down -v && docker compose up --build`.

**Port already in use**
- Stop any existing processes on ports `27017`, `8000`, or `5000`, or edit the port mappings in `docker-compose.yml`.

---

## Directory Reference

```
EMS-CS-302/
├── train_model.py          # Step 2 — run this first to generate the model
├── predict_hospital.py     # Core ML logic (do not edit)
├── ml_service/             # FastAPI wrapper (port 8000)
├── backend/EMS.API/        # ASP.NET Core REST API + SignalR (port 5000)
├── flutter_app/            # Flutter mobile app (Step 8)
├── scripts/                # MongoDB init and seed scripts (auto-run by Docker)
├── models/                 # Generated model files (created by train_model.py)
├── datasets/               # Training data (obtain from team)
├── docker-compose.yml      # Wires all services together
└── .env.example            # Copy to .env and edit (Step 3)
```
