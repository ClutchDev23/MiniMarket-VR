<div align="center">

# 🛒 Cajero XR · MiniMarket VR

**Un minimarket que cabe en tu cabeza... ¡literalmente, porque es VR!** 🥽✨

![Unity](https://img.shields.io/badge/Unity-6000.4.9f1-000000?logo=unity&logoColor=white)
![OpenXR](https://img.shields.io/badge/OpenXR-1.16.1-5586A4)
![Platform](https://img.shields.io/badge/Meta%20Quest-APK-0081FB?logo=meta&logoColor=white)
![Platform](https://img.shields.io/badge/PC-Windows-0078D6?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

[Qué es](#-qué-es) · [Características](#-características) · [Controles](#-controles) · [Instalación](#-cómo-abrir-el-proyecto) · [Build](#-cómo-hacer-el-build) · [Scripts](#-scripts-principales) · [Roadmap](#%EF%B8%8F-roadmap) · [Autores](#-autores)

</div>

---

## 📖 Qué es

**Cajero XR** (repositorio *MiniMarket VR*) es un proyecto de realidad virtual hecho en **Unity 6** donde entras en primera persona 👀 a un minimarket virtual. Agarra productos, recorre los pasillos, escucha sonidos que cambian según dónde estés y siente que compras de verdad... sin hacer cola. 🎉

Corre en **Meta Quest** 🥽 y también en **PC** 🖥️. Puedes jugar con **controladores** o directamente con **tus manos** 🤲.

## ✨ Características

- 🏪 **Escena completa de minimarket**: pasillos, estantes, productos y más.
- 🎛️ **Menú VR propio** con pantallas de Jugar, Ajustes, Controles y Créditos, con iconos y diseño personalizado.
- 🤲 **Interacción con manos y controladores** (XR Interaction Toolkit + XR Hands).
- 🚶 **Locomoción por brazos**: camina moviendo los brazos, además del joystick.
- 🌙 **Ciclo día/noche** en la escena del juego.
- ⚙️ **Ajustes en vivo**: volumen, velocidad al caminar, caminar con brazos y calidad gráfica.
- 🔊 **Audio 3D**: los sonidos se disparan al pasar por zonas con triggers.
- 🎵 **Música de fondo**.
- 📦 **Build a Quest con un click** desde el menú de Unity.

## 🎮 Controles

| Acción | 🎮 Controladores | 🤲 Manos |
|---|---|---|
| **Agarrar** | Toca el objeto y aprieta **Grip** | Toca el objeto y cierra la mano |
| **Caminar** | Joystick izquierdo | Estira los brazos y muévelos |
| **Girar** | Joystick derecho | Gira tu cuerpo |

## 🛠️ Tech Stack

| Tecnología | Versión | Para qué |
|---|---|---|
| 🟦 Unity | `6000.4.9f1` | Motor del juego |
| 🟩 URP | `17.4.0` | Render pipeline optimizado |
| 🥽 XR Interaction Toolkit | `3.4.1` | Agarrar, teleport, manos y UI en VR |
| 🌐 OpenXR | `1.16.1` | Estándar XR (soporte Meta Quest) |
| 🤲 XR Hands | `1.7.3` | Seguimiento de manos |
| 🎮 Input System | `1.19.0` | Input moderno (solo Input System) |
| 🧠 Visual Scripting | `1.9.1` | Prototipado sin código |

## 📂 Estructura del proyecto

```
MiniMarket-VR/
├── Assets/
│   ├── Scenes/        # Menu.unity, MinimarketXR.unity, MinimarketXR_Full.unity
│   ├── Scripts/       # Scripts de runtime (menú, ajustes, locomoción, audio...)
│   ├── Editor/        # Herramientas de editor (builds, audios 3D, generador de menú...)
│   ├── Icons/         # Iconos PNG del menú VR
│   ├── SuperMarket/   # Modelos, materiales, prefabs y texturas
│   ├── Audio/         # Sonidos del juego
│   ├── XR/ y XRI/     # Configuración XR
│   └── Samples/       # Samples de XR Interaction Toolkit
├── Builds/
│   ├── Quest/         # MiniMarketVR.apk
│   └── PC/            # MiniMarketVR.exe
├── Packages/          # Dependencias (manifest.json)
└── ProjectSettings/   # Ajustes del proyecto
```

## 🚀 Cómo abrir el proyecto

> ⚠️ Este repositorio usa **Git LFS** para los archivos pesados. Instálalo antes de clonar: https://git-lfs.com

1. 📥 Instala Git LFS (una sola vez):
   ```bash
   git lfs install
   ```
2. 📥 Clona el repo:
   ```bash
   git clone https://github.com/ClutchDev23/MiniMarket-VR.git
   ```
3. 🟦 Ábrelo con **Unity 6000.4.9f1** (Unity Hub → *Add* → selecciona la carpeta).
4. ⏳ Espera a que compile. El primer load siempre es eterno ☕
5. ▶️ Abre la escena **`Menu`** y dale Play para probar el flujo completo, o abre **`MinimarketXR_Full`** para ir directo al juego.

## 📦 Cómo hacer el build

### 🥽 Quest (APK)

En el menú de Unity: **Tools → Minimarket → Build Quest APK**

Genera el APK en `Builds/Quest/MiniMarketVR.apk` (IL2CPP + ARM64, como pide Quest). 🎉

> 💡 El nombre que aparece en la Biblioteca del Quest es el **Product Name** (*Edit → Project Settings → Player*). Cámbialo a `Cajero XR`. Esto no modifica el Package Name, así que SideQuest sigue actualizando la misma app.

### 🖥️ PC

**File → Build Settings → Windows → Build**, o usa el build existente en `Builds/PC/MiniMarketVR.exe`.

> 📋 En *Build Settings* la escena **`Menu`** debe ir primero (índice 0) y **`MinimarketXR_Full`** después.

## 🎛️ Regenerar el menú VR

El menú se construye por código con una herramienta de editor, así que puedes cambiar textos, colores e iconos sin armar nada a mano:

1. Abre la escena **`Menu`**.
2. **Tools → MiniMarket → Crear Menu VR** → *Continuar* (reemplaza el menú anterior).
3. Revisa que `MenuManager → Game Scene` sea `MinimarketXR_Full` y guarda con `Ctrl + S`.

Todo lo editable está al inicio de `Assets/Editor/MenuBuilder.cs`:

| Qué | Dónde |
|---|---|
| Nombre, subtítulo, versión y créditos | `GameName`, `Subtitle`, `Version`, `Dev1`, `Dev2` |
| Colores | `Accent`, `BtnMain`, `BtnSecondary`, `BtnDanger`, `PanelColor`... |
| Iconos | PNG en `Assets/Icons/` (mismo nombre = se reemplaza) |

## 🧩 Scripts principales

### Runtime (`Assets/Scripts/`)

| Script | Qué hace |
|---|---|
| `MainMenu` | Controla los paneles del menú (Jugar, Ajustes, Controles, Créditos, Salir) |
| `SettingsApplier` | Aplica los ajustes elegidos en el menú durante el juego |
| `ArmSwingLocomotion` | Camina moviendo los brazos |
| `DayNightCycle` | Ciclo día/noche de la escena |
| `TriggerAudio` | Reproduce un audio cuando el jugador (XR Origin) entra en una zona |
| `SimulatorSoloEnEditor` | Activa el simulador XR solo en el editor (nunca en builds) |

### Editor (`Assets/Editor/`)

| Script | Qué hace |
|---|---|
| `MenuBuilder` | Genera el menú VR completo desde **Tools → MiniMarket** |
| `BuildQuest` | Automatiza el build de Quest desde el menú |
| `CrearAudios3D` | Crea/configura audios 3D masivamente |
| `HornearLighting` | Hornea la iluminación (bake) de las escenas |
| `ReemplazarRigXR` | Reemplaza rigs por rigs compatibles con XR |

## 🗺️ Roadmap

- [x] 🎛️ Menú VR (Jugar, Ajustes, Controles, Créditos)
- [x] 🚶 Locomoción por brazos
- [x] 🌙 Ciclo día/noche
- [ ] 🛒 Carrito de compras interactivo
- [ ] 💰 Sistema de caja/escáner
- [ ] 🏷️ Productos con precios y etiquetas
- [ ] 🤖 NPC vendedor
- [ ] 📱 Optimización extra para Quest

## 🤝 Contribuir

¿Quieres echar una mano? ¡Bienvenid@! 🙌

1. 🍴 Haz fork del repo.
2. 🌿 Crea una rama: `git checkout -b mi-feature`
3. 💾 Haz commits pequeños y claros: `git commit -m "Agregar carrito interactivo"`
4. 📤 Push y abre un Pull Request.

> 🧹 Tip: Unity a veces modifica archivos solo al abrir el proyecto (`TextMesh Pro`, `ProjectSettings/EditorSettings.asset`). Si no tocaste nada ahí, no los incluyas en tu commit.

## 👥 Autores

| | |
|---|---|
| 👨‍💻 **Abner Gonzales** | Desarrollo |
| 👨‍💻 **Hubert Hilario** | Desarrollo |

## 🙏 Créditos

- 📦 Modelos 3D: *Supermarket Props 3D Models Collection*
- 🛠️ Unity 6 · OpenXR · XR Interaction Toolkit
- 🥽 Plataforma: Meta Quest

## 📜 Licencia

Distribuido bajo la **Licencia MIT**. Haz lo que quieras, pero el crédito siempre se agradece. 😎

---

<div align="center">

Hecho con ❤️, café ☕ y mucho VR 🥽

**Cajero XR © 2026**

</div>
