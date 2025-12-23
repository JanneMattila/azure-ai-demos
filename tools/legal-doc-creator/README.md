# Legal Document Creator

```powershell
cd tools/legal-doc-creator

# Create virtual environment
python -m venv venv

# Activate virtual environment
.\venv\Scripts\Activate.ps1
# ./venv/Scripts/activate
# python3 -m venv venv

# Install dependencies
pip install -r requirements.txt

python main.py -h

python main.py generate
python main.py convert
```
