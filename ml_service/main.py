"""
main.py — FastAPI ML service for EMS hospital prediction.
Wraps HospitalPredictor from predictor.py as a REST API.

Usage:
    uvicorn ml_service.main:app --host 0.0.0.0 --port 8000
    # or from repo root:
    uvicorn main:app --host 0.0.0.0 --port 8000 --app-dir ml_service
"""

from __future__ import annotations

import os
from contextlib import asynccontextmanager
from typing import Annotated

from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware

from predictor import VALID_CONDITIONS, VALID_SEVERITIES, HospitalPredictor
from schemas import (
    EmsBaseInfo,
    HealthResponse,
    HospitalInfo,
    PredictRequest,
    PredictResponse,
)


# ─── App lifespan: load the model once at startup ────────────────────────────

predictor = HospitalPredictor()


@asynccontextmanager
async def lifespan(app: FastAPI):
    api_key = os.getenv("ORS_API_KEY")
    predictor.load(api_key=api_key)
    yield
    # Cleanup if needed


# ─── FastAPI app ──────────────────────────────────────────────────────────────

app = FastAPI(
    title="EMS Hospital Prediction API",
    description=(
        "Predicts the most appropriate hospital for an EMS dispatch in Marikina City "
        "using a Random Forest classifier. Wraps the existing predict_hospital.py model."
    ),
    version="1.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=os.getenv("ALLOWED_ORIGINS", "*").split(","),
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ─── Routes ───────────────────────────────────────────────────────────────────

@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """Liveness probe — returns whether the model is loaded."""
    return HealthResponse(
        status="ok",
        model_loaded=predictor.model is not None,
    )


@app.post("/predict", response_model=PredictResponse, tags=["Prediction"])
async def predict_hospital(body: PredictRequest):
    """
    Predict the most appropriate hospital for a patient given their location,
    case severity, and medical condition.

    - Finds the nearest EMS base (by travel time).
    - Calculates distance and travel time to every hospital.
    - Runs the Random Forest classifier.
    - Returns the predicted hospital plus full time-component breakdown.
    """
    if predictor.model is None:
        raise HTTPException(status_code=503, detail="ML model not loaded yet. Try again shortly.")

    try:
        result = predictor.predict(
            latitude=body.latitude,
            longitude=body.longitude,
            severity=body.severity,
            condition=body.condition,
        )
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Prediction failed: {exc}") from exc

    return PredictResponse(**result)


@app.get("/hospitals", response_model=list[HospitalInfo], tags=["Reference Data"])
async def list_hospitals():
    """Return all hospitals with coordinates from the hospital dataset."""
    if predictor.hospitals is None:
        raise HTTPException(status_code=503, detail="Hospital data not loaded yet.")
    return [HospitalInfo(**h) for h in predictor.get_hospitals()]


@app.get("/ems-bases", response_model=list[EmsBaseInfo], tags=["Reference Data"])
async def list_ems_bases():
    """Return all Marikina EMS bases with coordinates."""
    return [
        EmsBaseInfo(
            base_id=b["base_id"],
            base_name=b["base_name"],
            latitude=b["latitude"],
            longitude=b["longitude"],
        )
        for b in predictor.get_ems_bases()
    ]


@app.get("/meta/severities", tags=["Reference Data"])
async def list_severities() -> list[str]:
    """Return valid severity values accepted by /predict."""
    return VALID_SEVERITIES


@app.get("/meta/conditions", tags=["Reference Data"])
async def list_conditions() -> list[str]:
    """Return valid condition values accepted by /predict."""
    return VALID_CONDITIONS
