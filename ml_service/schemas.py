"""
schemas.py — Pydantic v2 request/response models for the FastAPI ML service.
"""

from __future__ import annotations

from typing import Optional
from pydantic import BaseModel, Field, field_validator


# ─── Request ──────────────────────────────────────────────────────────────────

class PredictRequest(BaseModel):
    latitude: float = Field(
        ...,
        ge=14.60, le=14.68,
        description="Patient latitude (Marikina City bounding box)",
        examples=[14.635],
    )
    longitude: float = Field(
        ...,
        ge=121.07, le=121.13,
        description="Patient longitude (Marikina City bounding box)",
        examples=[121.095],
    )
    severity: str = Field(
        ...,
        description="Case severity: low | medium | high",
        examples=["high"],
    )
    condition: str = Field(
        ...,
        description=(
            "Medical condition. One of: Minor injury, Fever, Laceration, Fracture, "
            "Moderate respiratory distress, Abdominal pain, Heart attack, "
            "Major trauma, Stroke"
        ),
        examples=["Heart attack"],
    )

    @field_validator("severity")
    @classmethod
    def validate_severity(cls, v: str) -> str:
        allowed = {"low", "medium", "high"}
        if v.lower() not in allowed:
            raise ValueError(f"severity must be one of {sorted(allowed)}")
        return v.lower()

    @field_validator("condition")
    @classmethod
    def validate_condition(cls, v: str) -> str:
        allowed = {
            "Minor injury", "Fever", "Laceration", "Fracture",
            "Moderate respiratory distress", "Abdominal pain",
            "Heart attack", "Major trauma", "Stroke",
        }
        if v not in allowed:
            raise ValueError(f"condition must be one of {sorted(allowed)}")
        return v


# ─── Response ─────────────────────────────────────────────────────────────────

class Coords(BaseModel):
    lat: float
    lng: float


class EmsBaseResult(BaseModel):
    base_id: int
    base_name: str
    coords: list[float]  # [lat, lng]
    is_road_distance: bool


class TimeComponents(BaseModel):
    dispatch_time: float
    time_to_patient: float
    on_scene_time: float
    time_to_hospital: float
    handover_time: float
    total_time: float


class PredictResponse(BaseModel):
    hospital_id: int
    hospital_name: str
    hospital_level: Optional[int]
    hospital_coords: Coords
    ems_base: EmsBaseResult
    distance_km: float
    time_components: TimeComponents
    is_fallback_calculation: bool


# ─── Auxiliary response models ────────────────────────────────────────────────

class HospitalInfo(BaseModel):
    hospital_id: int
    hospital_name: str
    hospital_level: Optional[int]
    latitude: float
    longitude: float


class EmsBaseInfo(BaseModel):
    base_id: int
    base_name: str
    latitude: float
    longitude: float


class HealthResponse(BaseModel):
    status: str
    model_loaded: bool
