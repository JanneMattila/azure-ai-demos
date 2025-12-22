"""
PDF Creator - Product Documentation Generator

This application generates product documentation for industrial equipment.
It works in two phases:
1. Generate markdown files into a subfolder for review/editing
2. Convert markdown files to PDF documents

Product numbers follow the format: PRD12345 (PRD + 5 digits)
"""

import os
import re
import random
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional
import markdown
from weasyprint import HTML, CSS
from weasyprint.text.fonts import FontConfiguration


# Configuration
MARKDOWN_OUTPUT_DIR = "markdown_output"
PDF_OUTPUT_DIR = "pdf_output"

# Product categories and their specifications
PRODUCT_CATEGORIES = {
    "Industrial Motors": {
        "prefix": "IM",
        "specs": {
            "Power Rating": ["0.75 kW", "1.5 kW", "2.2 kW", "3.7 kW", "5.5 kW", "7.5 kW", "11 kW", "15 kW", "22 kW", "37 kW"],
            "Voltage": ["230V AC", "400V AC", "690V AC"],
            "Frequency": ["50 Hz", "60 Hz", "50/60 Hz"],
            "Speed": ["1000 RPM", "1500 RPM", "3000 RPM"],
            "Efficiency Class": ["IE1", "IE2", "IE3", "IE4"],
            "Enclosure": ["IP55", "IP56", "IP65", "IP66"],
            "Mounting": ["B3 Foot", "B5 Flange", "B14 Face", "B35 Foot & Flange"],
            "Insulation Class": ["F", "H"],
            "Cooling Method": ["IC411 (TEFC)", "IC416 (Force Ventilated)"],
            "Frame Material": ["Cast Iron", "Aluminum Alloy"],
        }
    },
    "Hydraulic Pumps": {
        "prefix": "HP",
        "specs": {
            "Displacement": ["10 cc/rev", "16 cc/rev", "25 cc/rev", "40 cc/rev", "63 cc/rev", "100 cc/rev"],
            "Maximum Pressure": ["250 bar", "315 bar", "350 bar", "400 bar"],
            "Maximum Speed": ["1800 RPM", "2400 RPM", "3000 RPM", "3600 RPM"],
            "Fluid Type": ["Mineral Oil", "Synthetic Ester", "Water Glycol", "Phosphate Ester"],
            "Viscosity Range": ["10-100 cSt", "15-200 cSt", "20-400 cSt"],
            "Port Configuration": ["SAE-A 2 Bolt", "SAE-B 4 Bolt", "ISO 228-1"],
            "Shaft Type": ["Cylindrical", "Splined", "Keyed"],
            "Seal Material": ["NBR", "FKM (Viton)", "HNBR", "EPDM"],
        }
    },
    "Control Valves": {
        "prefix": "CV",
        "specs": {
            "Valve Type": ["Globe", "Ball", "Butterfly", "Gate", "Diaphragm"],
            "Size": ["DN15", "DN25", "DN50", "DN80", "DN100", "DN150", "DN200"],
            "Pressure Class": ["PN16", "PN25", "PN40", "PN63", "PN100"],
            "Body Material": ["Cast Steel", "Stainless Steel 316L", "Duplex Steel", "Hastelloy C"],
            "Trim Material": ["Stellite", "Tungsten Carbide", "Ceramic"],
            "Actuator Type": ["Pneumatic", "Electric", "Hydraulic", "Manual"],
            "Flow Characteristic": ["Linear", "Equal Percentage", "Quick Opening"],
            "Cv Range": ["0.1-1.0", "1.0-10", "10-100", "100-500"],
            "Temperature Range": ["-40°C to 200°C", "-20°C to 400°C", "0°C to 600°C"],
        }
    },
    "Sensors": {
        "prefix": "SN",
        "specs": {
            "Sensor Type": ["Pressure", "Temperature", "Level", "Flow", "Vibration", "Position"],
            "Output Signal": ["4-20mA", "0-10V", "HART", "Modbus RTU", "Profibus PA", "Foundation Fieldbus"],
            "Accuracy": ["±0.1%", "±0.25%", "±0.5%", "±1.0%"],
            "Process Connection": ["1/4\" NPT", "1/2\" NPT", "G1/4", "G1/2", "Tri-Clamp"],
            "Housing Material": ["316L Stainless Steel", "Aluminum", "PVDF"],
            "Ingress Protection": ["IP65", "IP67", "IP68", "IP69K"],
            "Hazardous Area": ["Zone 0", "Zone 1", "Zone 2", "Non-Hazardous"],
            "Temperature Range": ["-40°C to 85°C", "-20°C to 100°C", "-50°C to 150°C"],
        }
    },
    "Conveyor Systems": {
        "prefix": "CS",
        "specs": {
            "Belt Type": ["PVC", "PU", "Rubber", "Modular Plastic", "Metal Mesh"],
            "Belt Width": ["300mm", "500mm", "800mm", "1000mm", "1200mm", "1500mm"],
            "Conveyor Length": ["3m", "6m", "10m", "15m", "20m", "30m"],
            "Load Capacity": ["50 kg/m", "100 kg/m", "200 kg/m", "500 kg/m"],
            "Speed Range": ["0.1-0.5 m/s", "0.5-1.0 m/s", "1.0-2.0 m/s", "2.0-4.0 m/s"],
            "Drive Type": ["Head Drive", "Center Drive", "Multi-Drive"],
            "Frame Material": ["Painted Steel", "Galvanized Steel", "Stainless Steel 304", "Aluminum Profile"],
            "Guide Rails": ["Fixed", "Adjustable", "Side Guides with Rollers"],
        }
    },
    "PLCs": {
        "prefix": "PL",
        "specs": {
            "CPU Type": ["Compact", "Modular", "Rack-based", "Safety"],
            "Digital Inputs": ["8 DI", "16 DI", "32 DI", "64 DI"],
            "Digital Outputs": ["8 DO", "16 DO", "32 DO", "64 DO"],
            "Analog Inputs": ["4 AI", "8 AI", "16 AI"],
            "Analog Outputs": ["2 AO", "4 AO", "8 AO"],
            "Communication": ["Ethernet/IP", "PROFINET", "Modbus TCP", "EtherCAT", "CC-Link"],
            "Program Memory": ["256 KB", "512 KB", "1 MB", "2 MB", "4 MB"],
            "Scan Time": ["0.1 ms/1K", "0.5 ms/1K", "1.0 ms/1K"],
            "Operating Temperature": ["0°C to 55°C", "-25°C to 70°C"],
        }
    }
}

# Safety and compliance standards
SAFETY_STANDARDS = [
    "IEC 61508 (Functional Safety)",
    "ISO 13849 (Safety of Machinery)",
    "ATEX Directive 2014/34/EU",
    "IECEx Certification",
    "UL Listed",
    "CSA Certified",
    "CE Marking",
    "UKCA Marking",
    "RoHS Compliant",
    "REACH Compliant"
]

# Warranty terms
WARRANTY_OPTIONS = [
    "12 months from date of shipment",
    "18 months from date of shipment or 12 months from commissioning",
    "24 months from date of installation",
    "36 months extended warranty available"
]


def generate_product_number() -> str:
    """Generate a random product number in format PRD12345."""
    return f"PRD{random.randint(10000, 99999)}"


def get_random_category() -> tuple[str, dict]:
    """Get a random product category and its specifications."""
    category = random.choice(list(PRODUCT_CATEGORIES.keys()))
    return category, PRODUCT_CATEGORIES[category]


def generate_serial_number(prefix: str) -> str:
    """Generate a serial number for the product."""
    year = datetime.now().year
    return f"{prefix}-{year}-{random.randint(100000, 999999)}"


def generate_revision_history() -> list[dict]:
    """Generate revision history for the document."""
    base_date = datetime.now() - timedelta(days=random.randint(365, 1000))
    revisions = []
    
    revision_descriptions = [
        "Initial release",
        "Updated technical specifications",
        "Added safety information section",
        "Revised installation procedures",
        "Updated electrical diagrams",
        "Added troubleshooting guide",
        "Performance data updated",
        "Compliance certifications added"
    ]
    
    num_revisions = random.randint(2, 5)
    for i in range(num_revisions):
        rev_date = base_date + timedelta(days=i * random.randint(60, 180))
        revisions.append({
            "revision": f"Rev {chr(65 + i)}",
            "date": rev_date.strftime("%Y-%m-%d"),
            "description": revision_descriptions[i] if i < len(revision_descriptions) else "General updates",
            "author": random.choice(["J. Smith", "M. Johnson", "K. Williams", "R. Brown", "L. Davis"])
        })
    
    return revisions


def generate_markdown_content(product_number: str, category: str, specs: dict) -> str:
    """Generate comprehensive markdown content for a product."""
    
    serial_number = generate_serial_number(specs["prefix"])
    revision_history = generate_revision_history()
    current_revision = revision_history[-1]["revision"]
    
    # Select random specifications
    selected_specs = {}
    for spec_name, options in specs["specs"].items():
        selected_specs[spec_name] = random.choice(options)
    
    # Generate the markdown content
    content = f"""# Product Specification Document

---

## Document Information

| Field | Value |
|-------|-------|
| **Product Number** | {product_number} |
| **Serial Number** | {serial_number} |
| **Category** | {category} |
| **Document Revision** | {current_revision} |
| **Issue Date** | {datetime.now().strftime("%Y-%m-%d")} |
| **Classification** | Technical Documentation |

---

## Revision History

| Revision | Date | Description | Author |
|----------|------|-------------|--------|
"""
    
    for rev in revision_history:
        content += f"| {rev['revision']} | {rev['date']} | {rev['description']} | {rev['author']} |\n"
    
    content += f"""
---

## 1. Product Overview

### 1.1 Introduction

The {product_number} is a high-performance {category.lower()} unit designed for demanding industrial applications. This equipment has been engineered to meet the highest standards of reliability, efficiency, and safety in modern manufacturing and process industries.

### 1.2 Intended Use

This product is intended for use in:

- Manufacturing and assembly plants
- Process industries (chemical, pharmaceutical, food & beverage)
- Material handling systems
- Automated production lines
- Infrastructure and utilities

### 1.3 Key Features

- **High Reliability**: Designed for continuous operation in industrial environments
- **Energy Efficient**: Optimized for minimal power consumption
- **Easy Maintenance**: Modular design for quick serviceability
- **Robust Construction**: Built to withstand harsh operating conditions
- **Compliance**: Meets international safety and performance standards

---

## 2. Technical Specifications

### 2.1 General Specifications

| Parameter | Specification |
|-----------|---------------|
"""
    
    for spec_name, spec_value in selected_specs.items():
        content += f"| {spec_name} | {spec_value} |\n"
    
    content += f"""
### 2.2 Environmental Specifications

| Parameter | Value |
|-----------|-------|
| Operating Temperature | -20°C to +50°C |
| Storage Temperature | -40°C to +70°C |
| Relative Humidity | 5% to 95% (non-condensing) |
| Altitude | Up to 2000m above sea level |
| Vibration Resistance | 10-55 Hz, 0.15mm amplitude |
| Shock Resistance | 15g, 11ms half-sine pulse |

### 2.3 Electrical Specifications

| Parameter | Value |
|-----------|-------|
| Supply Voltage | According to nameplate |
| Voltage Tolerance | ±10% |
| Frequency Tolerance | ±2% |
| Power Factor | > 0.85 |
| EMC Compliance | EN 61000-6-2, EN 61000-6-4 |

---

## 3. Mechanical Dimensions

### 3.1 Overall Dimensions

```
    ┌─────────────────────────────────┐
    │                                 │
    │    ┌───────────────────────┐    │
    │    │                       │    │
    │    │      {product_number}      │    │
    │    │                       │    │
    │    │    MAIN ASSEMBLY      │    │
    │    │                       │    │
    │    └───────────────────────┘    │
    │                                 │
    └─────────────────────────────────┘
    
    Width (W):  {random.randint(200, 800)}mm
    Height (H): {random.randint(300, 1000)}mm
    Depth (D):  {random.randint(150, 600)}mm
    Weight:     {random.randint(15, 250)}kg
```

### 3.2 Mounting Requirements

- **Foundation**: Level concrete base with minimum thickness of 150mm
- **Anchor Bolts**: M12 or M16 grade 8.8 (depending on size)
- **Clearance**: Minimum 500mm on all sides for maintenance access
- **Ventilation**: Ensure adequate airflow for cooling

---

## 4. Installation Instructions

### 4.1 Pre-Installation Checklist

Before installation, verify the following:

- [ ] Inspect packaging for shipping damage
- [ ] Verify product matches purchase order specifications
- [ ] Confirm installation site meets environmental requirements
- [ ] Ensure all required utilities are available
- [ ] Review and understand all safety warnings
- [ ] Gather required tools and equipment

### 4.2 Required Tools

| Tool | Specification |
|------|---------------|
| Torque Wrench | 10-100 Nm range |
| Socket Set | Metric 8mm - 24mm |
| Multimeter | CAT III rated |
| Spirit Level | 600mm minimum |
| Lifting Equipment | Rated for equipment weight |

### 4.3 Installation Procedure

1. **Unpacking**
   - Remove all packaging materials carefully
   - Retain documentation and accessories
   - Document any visible damage immediately

2. **Positioning**
   - Use appropriate lifting equipment
   - Position unit on prepared foundation
   - Align with mating equipment/piping

3. **Securing**
   - Install anchor bolts to specified torque
   - Verify level in both axes
   - Install vibration isolation if required

4. **Connections**
   - Connect electrical supply per wiring diagram
   - Connect process piping/ducting as required
   - Install instrumentation connections

5. **Verification**
   - Perform insulation resistance test
   - Check rotation direction (motors)
   - Verify all connections are secure

---

## 5. Operation

### 5.1 Control Panel Overview

```
┌──────────────────────────────────────────────┐
│                CONTROL PANEL                  │
├──────────────────────────────────────────────┤
│  ┌────┐  ┌────┐  ┌────┐  ┌────┐  ┌────┐     │
│  │ P  │  │ R  │  │ F  │  │ A  │  │ M  │     │
│  │ WR │  │ UN │  │ LT │  │ LM │  │ AN │     │
│  └────┘  └────┘  └────┘  └────┘  └────┘     │
│   PWR    RUN    FAULT   ALARM   MAINT       │
├──────────────────────────────────────────────┤
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │          DISPLAY PANEL               │   │
│  │     [ STATUS: READY ]                │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  [START]  [STOP]  [RESET]  [MENU]           │
└──────────────────────────────────────────────┘
```

### 5.2 Operating Modes

| Mode | Description | Indicator |
|------|-------------|-----------|
| Standby | Unit powered, awaiting start command | PWR solid |
| Running | Normal operation | RUN solid |
| Warning | Minor fault, operation continues | ALM flashing |
| Fault | Major fault, unit stopped | FLT solid |
| Maintenance | Service mode active | MAINT solid |

### 5.3 Start-Up Procedure

1. Verify all pre-start checks are complete
2. Ensure emergency stop is released
3. Apply main power supply
4. Wait for system initialization (approx. 30 seconds)
5. Verify no fault conditions are present
6. Press START button to begin operation
7. Monitor parameters during initial operation

### 5.4 Shutdown Procedure

**Normal Shutdown:**
1. Press STOP button
2. Wait for unit to come to complete stop
3. Isolate main power if extended shutdown

**Emergency Shutdown:**
1. Press EMERGENCY STOP immediately
2. Investigate cause before restart
3. Follow restart procedure after clearing fault

---

## 6. Maintenance

### 6.1 Maintenance Schedule

| Interval | Task | Personnel |
|----------|------|-----------|
| Daily | Visual inspection | Operator |
| Weekly | Check for leaks, unusual noise | Operator |
| Monthly | Lubrication points (if applicable) | Technician |
| Quarterly | Electrical connections check | Electrician |
| Annually | Complete inspection and testing | Engineer |
| 5 Years | Major overhaul / bearing replacement | Specialist |

### 6.2 Lubrication Requirements

| Point | Lubricant Type | Quantity | Interval |
|-------|----------------|----------|----------|
| Main bearings | ISO VG 68 mineral oil | 50ml | 3 months |
| Gearbox (if fitted) | ISO VG 220 gear oil | Check level | Monthly |
| Seals | Food-grade grease | Light coat | 6 months |

### 6.3 Spare Parts

**Recommended Spare Parts Kit ({product_number}-SPK):**

| Part Number | Description | Quantity |
|-------------|-------------|----------|
| {product_number}-001 | Main seal kit | 2 |
| {product_number}-002 | Bearing set | 1 |
| {product_number}-003 | Gasket set | 2 |
| {product_number}-004 | Filter element | 3 |
| {product_number}-005 | Fuse set | 1 |

---

## 7. Troubleshooting

### 7.1 Common Issues and Solutions

| Symptom | Possible Cause | Corrective Action |
|---------|----------------|-------------------|
| Unit will not start | No power supply | Check main breaker and fuses |
| | Emergency stop active | Release and reset E-stop |
| | Control circuit fault | Check control fuses |
| Excessive vibration | Misalignment | Check and realign |
| | Worn bearings | Replace bearings |
| | Unbalanced load | Balance or reduce load |
| Overheating | Blocked ventilation | Clean air passages |
| | Overload condition | Reduce load or check sizing |
| | Ambient temperature high | Improve ventilation |
| Unusual noise | Mechanical wear | Inspect and replace worn parts |
| | Foreign object | Inspect and remove |
| | Insufficient lubrication | Top up lubricant |

### 7.2 Fault Codes

| Code | Description | Severity | Action Required |
|------|-------------|----------|-----------------|
| E001 | Overcurrent | Critical | Check load, inspect motor |
| E002 | Overtemperature | Critical | Allow cooling, check ventilation |
| E003 | Communication loss | Warning | Check wiring and connections |
| E004 | Sensor fault | Warning | Replace faulty sensor |
| E005 | Low pressure | Warning | Check supply and filters |
| E006 | Emergency stop | Critical | Investigate cause, reset |

---

## 8. Safety Information

### 8.1 Safety Warnings

⚠️ **WARNING: ELECTRICAL HAZARD**
- Disconnect and lock out power before servicing
- Only qualified electricians should perform electrical work
- Verify zero energy state before touching electrical components

⚠️ **WARNING: MECHANICAL HAZARD**
- Keep guards in place during operation
- Do not reach into moving machinery
- Use proper lockout/tagout procedures

⚠️ **WARNING: PRESSURE HAZARD**
- Depressurize system before disconnecting lines
- Use appropriate PPE when working with pressurized systems
- Check pressure gauges before opening

### 8.2 Personal Protective Equipment (PPE)

The following PPE is required when working with this equipment:

| Activity | Required PPE |
|----------|--------------|
| Operation | Safety glasses, safety footwear |
| Maintenance | Safety glasses, gloves, footwear, hard hat |
| Electrical work | Arc flash rated PPE per NFPA 70E |
| Confined space | Per confined space entry program |

### 8.3 Compliance and Certifications

This product complies with the following standards:

"""
    
    selected_standards = random.sample(SAFETY_STANDARDS, random.randint(4, 7))
    for standard in selected_standards:
        content += f"- {standard}\n"
    
    content += f"""
---

## 9. Warranty Information

### 9.1 Standard Warranty

{random.choice(WARRANTY_OPTIONS)}

### 9.2 Warranty Conditions

The warranty covers defects in materials and workmanship under normal use and service conditions. The warranty does not cover:

- Damage from improper installation or operation
- Normal wear and tear items
- Modifications not authorized by manufacturer
- Damage from neglect, accident, or abuse
- Operation outside specified parameters

### 9.3 Warranty Claims

To submit a warranty claim:

1. Contact technical support with product serial number
2. Describe the issue and provide supporting documentation
3. Await RMA (Return Material Authorization) number
4. Ship product prepaid to authorized service center
5. Include completed warranty claim form

---

## 10. Technical Support

### 10.1 Contact Information

| Service | Contact |
|---------|---------|
| Technical Hotline | +1-800-555-0199 |
| Email Support | techsupport@example.com |
| Online Portal | support.example.com |
| Emergency (24/7) | +1-800-555-0911 |

### 10.2 Information Required for Support

When contacting technical support, please have the following information ready:

- Product number: **{product_number}**
- Serial number (from nameplate)
- Date of installation
- Description of issue
- Fault codes displayed
- Environmental conditions
- Recent changes or maintenance performed

---

## Appendix A: Wiring Diagrams

```
MAIN POWER CONNECTION
                                    
    L1 ────┬──── Main Contactor ────┬──── Motor/Load
           │                        │
    L2 ────┼─────────────────────────┼────
           │                        │
    L3 ────┼─────────────────────────┼────
           │                        │
    PE ────┴──── Ground Bus ────────┴──── Frame Ground
    
    
CONTROL CIRCUIT

    24VDC ────┬──── E-Stop ──── Start PB ──── Contactor Coil
              │                                    │
              └──── Stop PB (NC) ──────────────────┤
                                                   │
    0VDC ─────────────────────────────────────────┘
```

---

## Appendix B: Process & Instrumentation Diagram

```
                    ┌─────────┐
    Supply ────────►│  FI-001 │──────┐
                    │  (Flow) │      │
                    └─────────┘      │
                                     ▼
                              ┌────────────┐
                              │            │
                    ┌────────►│  {product_number}  │────────┐
                    │         │            │        │
                    │         └────────────┘        │
                    │               │               │
              ┌─────┴─────┐   ┌────┴────┐    ┌────┴─────┐
              │   PI-001  │   │  TI-001 │    │  PI-002  │
              │ (Pressure)│   │ (Temp)  │    │(Pressure)│
              └───────────┘   └─────────┘    └──────────┘
                                                   │
                                                   ▼
                                              To Process
```

---

## Appendix C: Dimensional Drawing

```
                    ────────── W ──────────
                   │                       │
              ┌────┴───────────────────────┴────┐
              │    ○                       ○    │  ─┐
              │                                 │   │
              │         ┌───────────┐           │   │
          ┌───┤         │           │           ├───┤
          │   │         │   MAIN    │           │   │  H
          │   │         │   UNIT    │           │   │
          └───┤         │           │           ├───┤
              │         └───────────┘           │   │
              │    ○                       ○    │  ─┘
              └────┬───────────────────────┬────┘
                   │                       │
                   └─────────────┬─────────┘
                                 │
                                 D
                                 
    Mounting Hole Pattern: 4x M12, PCD = {random.randint(150, 400)}mm
```

---

**Document End**

*This document is confidential and proprietary. Reproduction without written consent is prohibited.*

*© {datetime.now().year} Contoso Corporation. All rights reserved.*
"""
    
    return content


def create_markdown_files(num_products: int = 5) -> list[str]:
    """Generate markdown files for the specified number of products."""
    
    # Create output directory
    output_dir = Path(MARKDOWN_OUTPUT_DIR)
    output_dir.mkdir(exist_ok=True)
    
    generated_files = []
    product_numbers = set()
    
    print(f"\n{'='*60}")
    print("PHASE 1: Generating Markdown Files")
    print(f"{'='*60}\n")
    
    for i in range(num_products):
        # Generate unique product number
        while True:
            product_number = generate_product_number()
            if product_number not in product_numbers:
                product_numbers.add(product_number)
                break
        
        # Get random category
        category, specs = get_random_category()
        
        # Generate content
        content = generate_markdown_content(product_number, category, specs)
        
        # Write to file
        filename = f"{product_number}_specification.md"
        filepath = output_dir / filename
        
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        
        generated_files.append(str(filepath))
        print(f"  ✓ Generated: {filename} ({category})")
    
    print(f"\n{'='*60}")
    print(f"Generated {len(generated_files)} markdown files in '{MARKDOWN_OUTPUT_DIR}/'")
    print("You can now edit these files before converting to PDF.")
    print(f"{'='*60}\n")
    
    return generated_files


def get_pdf_css() -> str:
    """Return CSS styling for PDF generation."""
    return """
    @page {
        size: A4;
        margin: 2cm;
        @top-right {
            content: "Page " counter(page) " of " counter(pages);
            font-size: 9pt;
            color: #666;
        }
        @bottom-center {
            content: "CONFIDENTIAL - Industrial Equipment Documentation";
            font-size: 8pt;
            color: #999;
        }
    }
    
    body {
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        font-size: 10pt;
        line-height: 1.5;
        color: #333;
    }
    
    h1 {
        color: #1a5276;
        font-size: 24pt;
        border-bottom: 3px solid #1a5276;
        padding-bottom: 10px;
        page-break-after: avoid;
    }
    
    h2 {
        color: #2874a6;
        font-size: 16pt;
        margin-top: 20pt;
        border-bottom: 1px solid #2874a6;
        padding-bottom: 5px;
        page-break-after: avoid;
    }
    
    h3 {
        color: #3498db;
        font-size: 12pt;
        margin-top: 15pt;
        page-break-after: avoid;
    }
    
    table {
        width: 100%;
        border-collapse: collapse;
        margin: 15px 0;
        font-size: 9pt;
    }
    
    th {
        background-color: #2874a6;
        color: white;
        padding: 8px;
        text-align: left;
        font-weight: bold;
    }
    
    td {
        padding: 6px 8px;
        border: 1px solid #ddd;
    }
    
    tr:nth-child(even) {
        background-color: #f8f9fa;
    }
    
    tr:hover {
        background-color: #e8f4f8;
    }
    
    code, pre {
        font-family: 'Consolas', 'Courier New', monospace;
        background-color: #f4f4f4;
        border: 1px solid #ddd;
        border-radius: 3px;
    }
    
    code {
        padding: 2px 5px;
        font-size: 9pt;
    }
    
    pre {
        padding: 15px;
        overflow-x: auto;
        font-size: 8pt;
        line-height: 1.4;
        page-break-inside: avoid;
    }
    
    ul, ol {
        margin-left: 20px;
    }
    
    li {
        margin-bottom: 5px;
    }
    
    hr {
        border: none;
        border-top: 1px solid #ccc;
        margin: 20px 0;
    }
    
    blockquote {
        border-left: 4px solid #3498db;
        margin: 15px 0;
        padding: 10px 20px;
        background-color: #f8f9fa;
    }
    
    strong {
        color: #1a5276;
    }
    
    /* Warning boxes */
    p:has(⚠️), p:contains('WARNING') {
        background-color: #fff3cd;
        border: 1px solid #ffc107;
        border-left: 4px solid #ff9800;
        padding: 10px;
        margin: 15px 0;
    }
    
    /* Page breaks */
    h2 {
        page-break-before: auto;
    }
    
    table, pre, img {
        page-break-inside: avoid;
    }
    """


def convert_markdown_to_pdf(markdown_file: str, output_dir: Optional[str] = None) -> str:
    """Convert a single markdown file to PDF."""
    
    if output_dir is None:
        output_dir = PDF_OUTPUT_DIR
    
    # Create output directory
    Path(output_dir).mkdir(exist_ok=True)
    
    # Read markdown content
    with open(markdown_file, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    # Convert markdown to HTML
    html_content = markdown.markdown(
        md_content,
        extensions=['tables', 'fenced_code', 'toc', 'attr_list']
    )
    
    # Wrap in HTML document
    full_html = f"""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="utf-8">
        <title>Product Specification</title>
    </head>
    <body>
        {html_content}
    </body>
    </html>
    """
    
    # Generate PDF filename
    md_filename = Path(markdown_file).stem
    pdf_filename = f"{md_filename}.pdf"
    pdf_path = Path(output_dir) / pdf_filename
    
    # Configure fonts
    font_config = FontConfiguration()
    
    # Create PDF
    HTML(string=full_html).write_pdf(
        str(pdf_path),
        stylesheets=[CSS(string=get_pdf_css(), font_config=font_config)],
        font_config=font_config
    )
    
    return str(pdf_path)


def convert_all_markdown_to_pdf() -> list[str]:
    """Convert all markdown files in the markdown output directory to PDF."""
    
    markdown_dir = Path(MARKDOWN_OUTPUT_DIR)
    
    if not markdown_dir.exists():
        print(f"Error: Markdown directory '{MARKDOWN_OUTPUT_DIR}' does not exist.")
        print("Please run Phase 1 first to generate markdown files.")
        return []
    
    markdown_files = list(markdown_dir.glob("*.md"))
    
    if not markdown_files:
        print(f"No markdown files found in '{MARKDOWN_OUTPUT_DIR}/'")
        return []
    
    print(f"\n{'='*60}")
    print("PHASE 2: Converting Markdown to PDF")
    print(f"{'='*60}\n")
    
    generated_pdfs = []
    
    for md_file in markdown_files:
        try:
            pdf_path = convert_markdown_to_pdf(str(md_file))
            generated_pdfs.append(pdf_path)
            print(f"  ✓ Converted: {md_file.name} → {Path(pdf_path).name}")
        except Exception as e:
            print(f"  ✗ Error converting {md_file.name}: {str(e)}")
    
    print(f"\n{'='*60}")
    print(f"Generated {len(generated_pdfs)} PDF files in '{PDF_OUTPUT_DIR}/'")
    print(f"{'='*60}\n")
    
    return generated_pdfs


def list_markdown_files() -> list[str]:
    """List all markdown files in the output directory."""
    markdown_dir = Path(MARKDOWN_OUTPUT_DIR)
    
    if not markdown_dir.exists():
        return []
    
    return [str(f) for f in markdown_dir.glob("*.md")]


def list_pdf_files() -> list[str]:
    """List all PDF files in the output directory."""
    pdf_dir = Path(PDF_OUTPUT_DIR)
    
    if not pdf_dir.exists():
        return []
    
    return [str(f) for f in pdf_dir.glob("*.pdf")]


def print_menu():
    """Print the main menu."""
    print("\n" + "="*60)
    print("   PDF CREATOR - Product Documentation Generator")
    print("="*60)
    print("\nAvailable Commands:")
    print("-"*60)
    print("  1. Generate markdown files (Phase 1)")
    print("  2. Convert markdown to PDF (Phase 2)")
    print("  3. List generated markdown files")
    print("  4. List generated PDF files")
    print("  5. Run full pipeline (Phase 1 + Phase 2)")
    print("  6. Exit")
    print("-"*60)


def main():
    """Main entry point for the PDF Creator application."""
    
    print("\n" + "="*60)
    print("  Welcome to PDF Creator")
    print("  Product Documentation Generator")
    print("="*60)
    
    while True:
        print_menu()
        
        try:
            choice = input("\nEnter your choice (1-6): ").strip()
            
            if choice == "1":
                try:
                    num = int(input("How many products to generate? [5]: ").strip() or "5")
                    create_markdown_files(num)
                except ValueError:
                    print("Invalid number. Using default (5).")
                    create_markdown_files(5)
                    
            elif choice == "2":
                convert_all_markdown_to_pdf()
                
            elif choice == "3":
                files = list_markdown_files()
                if files:
                    print(f"\nMarkdown files in '{MARKDOWN_OUTPUT_DIR}/':")
                    for f in files:
                        print(f"  - {Path(f).name}")
                else:
                    print(f"\nNo markdown files found. Run Phase 1 first.")
                    
            elif choice == "4":
                files = list_pdf_files()
                if files:
                    print(f"\nPDF files in '{PDF_OUTPUT_DIR}/':")
                    for f in files:
                        print(f"  - {Path(f).name}")
                else:
                    print(f"\nNo PDF files found. Run Phase 2 first.")
                    
            elif choice == "5":
                try:
                    num = int(input("How many products to generate? [5]: ").strip() or "5")
                except ValueError:
                    num = 5
                create_markdown_files(num)
                input("\nPress Enter to continue to PDF generation (or edit files first)...")
                convert_all_markdown_to_pdf()
                
            elif choice == "6":
                print("\nThank you for using PDF Creator. Goodbye!\n")
                break
                
            else:
                print("\nInvalid choice. Please enter a number between 1 and 6.")
                
        except KeyboardInterrupt:
            print("\n\nOperation cancelled by user.")
            break
        except Exception as e:
            print(f"\nError: {str(e)}")


if __name__ == "__main__":
    main()
