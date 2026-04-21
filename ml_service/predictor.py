"""
predictor.py — Clean extraction of HospitalPredictor logic from predict_hospital.py.
All interactive I/O (input(), print(), subprocess, visualize_route) has been removed.
The original predict_hospital.py is left untouched.
"""

from __future__ import annotations

import pickle
import time
from math import atan2, cos, radians, sin, sqrt
from pathlib import Path

import numpy as np
import pandas as pd
import requests


# ─── Paths (relative to repo root, resolved at runtime) ──────────────────────
_REPO_ROOT = Path(__file__).resolve().parent.parent
MODELS_DIR = _REPO_ROOT / "models"
DATASETS_DIR = _REPO_ROOT / "datasets" / "hospital"
HOSPITAL_CSV = DATASETS_DIR / "hospital_dataset (cleaned).csv"


# ─── EMS Base definitions (mirrors predict_hospital.py) ──────────────────────
EMS_BASES: list[dict] = [
    {"base_id": 163, "base_name": "163 Base - Barangay Hall IVC",                         "latitude": 14.6270218, "longitude": 121.0797032},
    {"base_id": 166, "base_name": "166 Base - CHO Office, Barangay Sto. Nino",            "latitude": 14.6399746, "longitude": 121.0965973},
    {"base_id": 167, "base_name": "167 Base - Barangay Hall Kalumpang",                   "latitude": 14.624179,  "longitude": 121.0933239},
    {"base_id": 164, "base_name": "164 Base - DRRMO Building, Barangay Fortune",          "latitude": 14.6628689, "longitude": 121.1214235},
    {"base_id": 165, "base_name": "165 Base - St. Benedict Barangay Nangka",              "latitude": 14.6737274, "longitude": 121.108795},
    {"base_id": 169, "base_name": "169 Base - Pugad Lawin, Barangay Fortune",             "latitude": 14.6584306, "longitude": 121.1312048},
]

MARIKINA_BBOX = {
    "lat_min": 14.60, "lat_max": 14.68,
    "lon_min": 121.07, "lon_max": 121.13,
}

VALID_SEVERITIES = ["low", "medium", "high"]
VALID_CONDITIONS = [
    "Minor injury", "Fever", "Laceration",
    "Fracture", "Moderate respiratory distress", "Abdominal pain",
    "Heart attack", "Major trauma", "Stroke",
]

ORS_BASE_URL = "https://api.openrouteservice.org/v2/directions/driving-car"
AVERAGE_SPEED_KMH = 30.0


# ─── Distance helpers ─────────────────────────────────────────────────────────

def _haversine(coord1: list[float], coord2: list[float]) -> float:
    """Great-circle distance in km between two [lat, lon] points."""
    R = 6371.0
    lat1, lon1 = map(radians, coord1)
    lat2, lon2 = map(radians, coord2)
    dlat = lat2 - lat1
    dlon = lon2 - lon1
    a = sin(dlat / 2) ** 2 + cos(lat1) * cos(lat2) * sin(dlon / 2) ** 2
    return R * 2 * atan2(sqrt(a), sqrt(1 - a))


def _get_route_info(
    start: list[float], end: list[float], api_key: str | None
) -> tuple[float, float, bool]:
    """
    Returns (distance_km, duration_min, used_road_network).
    Falls back to haversine + constant speed when ORS is unavailable.
    """
    if api_key and api_key != "your_api_key_here":
        payload = {"coordinates": [[start[1], start[0]], [end[1], end[0]]]}
        headers = {"Authorization": api_key, "Content-Type": "application/json; charset=utf-8"}
        try:
            resp = requests.post(ORS_BASE_URL, headers=headers, json=payload, timeout=10)
            if resp.status_code == 200:
                summary = resp.json()["routes"][0]["summary"]
                return summary["distance"] / 1000, summary["duration"] / 60, True
        except Exception:
            pass  # fall through to haversine

    dist = _haversine(start, end)
    return dist, (dist / AVERAGE_SPEED_KMH) * 60, False


# ─── HospitalPredictor ────────────────────────────────────────────────────────

class HospitalPredictor:
    """
    Stateless prediction class.
    Call load() once at startup, then predict() for each request.
    """

    def __init__(self) -> None:
        self.model = None
        self.le_severity = None
        self.le_condition = None
        self.hospitals: pd.DataFrame | None = None

    # ── Lifecycle ──────────────────────────────────────────────────────────────

    def load(self, api_key: str | None = None) -> None:
        """Load pkl models and hospital CSV. Raises on failure."""
        with open(MODELS_DIR / "hospital_prediction_model.pkl", "rb") as f:
            self.model = pickle.load(f)
        with open(MODELS_DIR / "le_severity.pkl", "rb") as f:
            self.le_severity = pickle.load(f)
        with open(MODELS_DIR / "le_condition.pkl", "rb") as f:
            self.le_condition = pickle.load(f)

        self.hospitals = pd.read_csv(HOSPITAL_CSV)
        self.hospitals["location"] = self.hospitals[["Latitude", "Longtitude"]].values.tolist()
        self._api_key = api_key

    # ── Public API ─────────────────────────────────────────────────────────────

    def get_ems_bases(self) -> list[dict]:
        return EMS_BASES

    def get_hospitals(self) -> list[dict]:
        if self.hospitals is None:
            raise RuntimeError("Predictor not loaded. Call load() first.")
        rows = []
        for _, row in self.hospitals.iterrows():
            rows.append({
                "hospital_id": int(row["ID"]),
                "hospital_name": row["Name"],
                "hospital_level": int(row["Level"]) if "Level" in self.hospitals.columns else None,
                "latitude": float(row["Latitude"]),
                "longitude": float(row["Longtitude"]),
            })
        return rows

    def predict(
        self,
        latitude: float,
        longitude: float,
        severity: str,
        condition: str,
    ) -> dict:
        """
        Full prediction pipeline.  Returns a result dict matching the plan's
        /predict response schema.
        """
        if self.model is None:
            raise RuntimeError("Predictor not loaded. Call load() first.")

        patient_loc = [latitude, longitude]

        # 1. Find closest EMS base
        closest_base = self._closest_ems_base(patient_loc)

        # 2. Calculate patient → hospital distances
        hospital_info = self._hospital_distances(patient_loc)

        # 3. Run RF prediction
        return self._predict_hospital(
            latitude, longitude, severity, condition,
            closest_base, hospital_info,
        )

    # ── Private helpers ────────────────────────────────────────────────────────

    def _closest_ems_base(self, patient_loc: list[float]) -> dict:
        best = None
        for base in EMS_BASES:
            base_coords = [base["latitude"], base["longitude"]]
            dist, travel_time, is_road = _get_route_info(base_coords, patient_loc, self._api_key)
            entry = {**base, "coords": base_coords, "distance": dist,
                     "time": travel_time, "is_road_distance": is_road}
            if best is None or entry["time"] < best["time"]:
                best = entry
        return best

    def _hospital_distances(self, patient_loc: list[float]) -> list[tuple]:
        results = []
        for _, row in self.hospitals.iterrows():
            h_coords = row["location"]
            dist, travel_time, is_road = _get_route_info(patient_loc, h_coords, self._api_key)
            results.append((int(row["ID"]), dist, travel_time, not is_road))
            time.sleep(0.05)  # light throttle for ORS
        return results

    def _predict_hospital(
        self,
        latitude: float,
        longitude: float,
        severity: str,
        condition: str,
        closest_base: dict,
        hospital_info: list[tuple],
    ) -> dict:
        closest_hosp = min(hospital_info, key=lambda x: x[1])
        distance_km = closest_hosp[1]
        time_to_patient = closest_base["time"]
        time_to_hospital = closest_hosp[2]

        dispatch_time = 2.0
        on_scene_time = 10.0
        handover_time = 5.0
        total_time = dispatch_time + time_to_patient + on_scene_time + time_to_hospital + handover_time

        new_patient = pd.DataFrame({
            "latitude": [latitude],
            "longitude": [longitude],
            "severity": [severity],
            "condition": [condition],
            "distance_to_hospital_km": [distance_km],
            "response_time_min": [total_time],
        })
        new_patient["severity"] = self.le_severity.transform(new_patient["severity"])
        new_patient["condition"] = self.le_condition.transform(new_patient["condition"])

        predicted_id = int(self.model.predict(new_patient)[0])

        h_row = self.hospitals[self.hospitals["ID"] == predicted_id].iloc[0]
        hospital_level = int(h_row["Level"]) if "Level" in self.hospitals.columns else None

        return {
            "hospital_id": predicted_id,
            "hospital_name": h_row["Name"],
            "hospital_level": hospital_level,
            "hospital_coords": {
                "lat": float(h_row["Latitude"]),
                "lng": float(h_row["Longtitude"]),
            },
            "ems_base": {
                "base_id": closest_base["base_id"],
                "base_name": closest_base["base_name"],
                "coords": closest_base["coords"],
                "is_road_distance": closest_base["is_road_distance"],
            },
            "distance_km": round(distance_km, 3),
            "time_components": {
                "dispatch_time": dispatch_time,
                "time_to_patient": round(time_to_patient, 2),
                "on_scene_time": on_scene_time,
                "time_to_hospital": round(time_to_hospital, 2),
                "handover_time": handover_time,
                "total_time": round(total_time, 2),
            },
            "is_fallback_calculation": not closest_base["is_road_distance"],
        }
