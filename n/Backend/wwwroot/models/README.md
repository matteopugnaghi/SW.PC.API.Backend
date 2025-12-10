# 📁 3D Models Directory# Put your .glb, .gltf, .obj, or .stl model files here



This directory contains the 3D model files for **THIS PROJECT ONLY**.This directory will be served by the API at `/api/models` endpoints.



## 🎯 One Backend = One Project## Supported Formats:

- GLB (Binary GLTF)

**⚠️ IMPORTANT**: Each backend installation on an industrial PC serves **ONE SINGLE PROJECT**.- GLTF (JSON + bin)

- OBJ (Wavefront)

```- STL (Stereolithography)

Industrial PC (Site A) → Backend Instance → ONE ProjectConfig.xlsx → Models for Project A

Industrial PC (Site B) → Backend Instance → ONE ProjectConfig.xlsx → Models for Project B## Example files you can add:

Industrial PC (Site C) → Backend Instance → ONE ProjectConfig.xlsx → Models for Project C- cube.glb

```- model.gltf

- object.obj

## 📂 File Structure- part.stl

Place all GLB/GLTF files directly here:

```
models/
├── machine_main.glb
├── conveyor.glb
├── robot_arm.glb
├── tank_storage.glb
└── valve_assembly.glb
```

**No subdirectories needed** - All models for this project go directly in `models/`

## 🎨 Supported Formats

| Format | Extension | Recommended |
|--------|-----------|-------------|
| **GLB** | `.glb` | ✅ **YES** - Binary, compact |
| GLTF | `.gltf` + `.bin` | ⚠️ Use for development |
| OBJ | `.obj` + `.mtl` | ⚠️ Limited features |
| STL | `.stl` | ⚠️ Geometry only |
| FBX | `.fbx` | ⚠️ Requires conversion |

## 🌐 Access URLs

Models are served via HTTP on the local industrial network:

```
http://192.168.1.100:5000/models/machine_main.glb
http://192.168.1.100:5000/models/conveyor.glb
http://192.168.1.100:5000/models/tank_storage.glb
```

(Replace IP with your industrial PC's IP address)

## ⚙️ Configuration

Configure models in Excel file: `ExcelConfigs/ProjectConfig.xlsx`  
Sheet: **`3D_Models`**

Example:
```
| ModelId | ModelName     | FileName         | FileType | ... |
|---------|---------------|------------------|----------|-----|
| MDL001  | Main Machine  | machine_main.glb | glb      | ... |
| MDL002  | Conveyor Belt | conveyor.glb     | glb      | ... |
| MDL003  | Storage Tank  | tank_storage.glb | glb      | ... |
```

## 🏭 Multi-Site Example

### Site A - Madrid Factory
```
PC: 192.168.1.100
Project: "Madrid Production Line"
Models: envasadora.glb, transportador.glb
Excel: ProjectConfig.xlsx
```

### Site B - Barcelona Factory
```
PC: 192.168.1.100 (different network)
Project: "Barcelona Packaging"
Models: robot_paletizador.glb, cinta_salida.glb
Excel: ProjectConfig.xlsx
```

### Site C - Valencia Factory
```
PC: 192.168.1.100 (different network)
Project: "Valencia Tank Control"
Models: tanque_principal.glb, valvulas.glb
Excel: ProjectConfig.xlsx
```

**Each site has its own backend installation - completely independent.**

## 📤 Adding New Models

1. Export your 3D model as GLB (recommended)
2. Copy file to `wwwroot/models/` on the industrial PC
3. Add entry in `ExcelConfigs/ProjectConfig.xlsx` → Sheet `3D_Models`
4. Restart backend (if needed)
5. HMI will load the model automatically

## 🔗 Integration with HMI

The frontend HMI (React) loads models from the local backend:

```javascript
// Frontend code
const models = await fetch('http://192.168.1.100:5000/api/models');
// Returns list of all models configured in ProjectConfig.xlsx

// Load specific model
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader';
loader.load('http://192.168.1.100:5000/models/machine_main.glb', (gltf) => {
  scene.add(gltf.scene);
});
```

## 📖 Full Documentation

See `3D_MODELS_README.md` for complete documentation including:
- PLC variable bindings for animations
- Camera configuration
- Model optimization
- Troubleshooting

---

**Current Location**: `wwwroot/models/`  
**Configuration**: `ExcelConfigs/ProjectConfig.xlsx`  
**One Backend = One Industrial PC = One Project**
