"""
System diagnostics utilities for monitoring CPU, memory, and disk usage.
"""

import psutil


def get_cpu_usage() -> float:
    """Get current CPU usage percentage."""
    return psutil.cpu_percent(interval=0.1)


def get_memory_usage() -> dict:
    """Get current memory usage statistics."""
    mem = psutil.virtual_memory()
    return {
        "percent": mem.percent,
        "used": mem.used,
        "total": mem.total,
        "available": mem.available,
    }


def get_disk_usage() -> dict:
    """Get current disk usage statistics for root partition."""
    disk = psutil.disk_usage("/")
    return {
        "percent": disk.percent,
        "used": disk.used,
        "total": disk.total,
        "free": disk.free,
    }


def get_system_diagnostics() -> dict:
    """
    Get complete system diagnostics including CPU, memory, and disk usage.

    Returns:
        dict: Contains cpuUsage, memoryUsage, and diskUsage
    """
    return {
        "cpuUsage": get_cpu_usage(),
        "memoryUsage": get_memory_usage(),
        "diskUsage": get_disk_usage(),
    }
