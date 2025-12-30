#!/usr/bin/env python3
"""
Startup script for The Island game backend.
Runs the FastAPI server with uvicorn and hot-reloading enabled.
"""

import uvicorn

if __name__ == "__main__":
    uvicorn.run(
        "backend.app.main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info"
    )
