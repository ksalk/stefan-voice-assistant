import sys
import os

# Add src/ to the Python path so imports like `from server import ...` work.
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))
