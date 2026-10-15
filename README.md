# 🛒 MiniMarket VR

> *Un minimarket que cabe en tu cabeza... ¡literalmente, porque es VR!* 🥽✨

![Unity](https://img.shields.io/badge/Unity-6000.4.9f1-000000?style=flat-square&logo=unity)
![OpenXR](https://img.shields.io/badge/OpenXR-ready-blue?style=flat-square)
![Quest](https://img.shields.io/badge/Meta-Quest-ready-000000?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

---

## 📖 ¿Qué es esto?

**MiniMarket VR** es un proyecto de realidad virtual hecho en **Unity 6** donde puedes meterte de lleno (y en primera persona 👀) a un minimarket virtual. Agarra productos, escucha sonidos al caminar por los pasillos y siente que de verdad estás comprando... sin hacer cola. 🎉

Corre en **Meta Quest** 🥽 y también en **PC** 🖥️, así que tú decides dónde comprar tus memes... digo, tus cosas.

---

## ✨ Características

- 🏪 Escena de minimarket completa (pasillos, estantes, productos y más)
- 🥽 Compatible con Meta Quest (Android / APK)
- 🖥️ Build para PC (Windows)
- 🤚 Interacción con manos y controladores (XR Interaction Toolkit)
- 🔊 Audio 3D: los sonidos se disparan cuando el jugador pasa por zonas con triggers
- 🎵 Música de fondo 🎶
- 📦 Sistema de build automático para Quest con un solo click del menú

---

## 🛠️ Tech Stack

| Tecnología | Versión | Para qué |
|---|---|---|
| 🟦 Unity | 6000.4.9f1 | El motor de todo |
| 🟩 URP | 17.4.0 | Render pipeline bonito y optimizado |
| 🥽 XR Interaction Toolkit | 3.4.1 | Agarrar, teleport, manos... |
| 🌐 OpenXR | 1.16.1 | Estándar XR (con soporte Meta Quest) |
| 🤲 XR Hands | 1.7.3 | Seguimiento de manos |
| 🎮 Input System | 1.19.0 | Input moderno (¡solo Input System!) |
| 🧠 Visual Scripting | 1.9.1 | Para prototipar sin código |

---

## 📂 Estructura del proyecto

```
📁 MiniMarket-VR/
├── 📁 Assets/
│   ├── 📁 Scenes/          # 🎬 MinimarketXR.unity y MinimarketXR_Full.unity
│   ├── 📁 Scripts/         # 📜 Scripts en runtime (TriggerAudio, etc.)
│   ├── 📁 Editor/          # 🔧 Herramientas de editor (build, audios 3D, etc.)
│   ├── 📁 SuperMarket/     # 🏪 Modelos, materiales, prefabs y texturas
│   ├── 📁 Audio/           # 🔊 Sonidos del juego
│   ├── 📁 XR/ y XRI/       # 🥽 Configuración XR
│   └── 📁 Samples/         # 📦 Samples de XR Interaction Toolkit
├── 📁 Builds/
│   ├── 📁 Quest/           # 🥽 MiniMarketVR.apk
│   └── 📁 PC/              # 🖥️ MiniMarketVR.exe
├── 📁 Packages/            # 📦 Dependencias (manifest.json)
└── 📁 ProjectSettings/     # ⚙️ Ajustes del proyecto
```

---

## 🚀 ¿Cómo abrir el proyecto?

1. 📥 Clona el repo:

```bash
git clone https://github.com/ClutchDev23/MiniMarket-VR.git
```

2. 🟦 Ábrelo con **Unity 6000.4.9f1** (Unity Hub → Add → selecciona la carpeta)
3. ⏳ Espera a que compile... sí, el primer load siempre es eterno ☕
4. ▶️ Dale play en la escena `MinimarketXR_Full`

---

## 📦 ¿Cómo hacer el build?

### 🥽 Para Quest (APK)

En el menú de Unity:

```
Tools → Minimarket → Build Quest APK
```

y listo, te genera el APK en `Builds/Quest/MiniMarketVR.apk` 🎉
*(Usa IL2CPP + ARM64, tal como corresponde para Quest)*

### 🖥️ Para PC

```
File → Build Settings → Windows → Build
```

o revisa el build ya existente en `Builds/PC/MiniMarketVR.exe` 🖱️

---

## 🧩 Scripts principales

| Script | 📍 Ubicación | 📝 Qué hace |
|---|---|---|
| `TriggerAudio` | `Assets/Scripts/` | 🔊 Reproduce un audio cuando el jugador (XR Origin) entra en una zona |
| `SimulatorSoloEnEditor` | `Assets/Scripts/` | 🧪 Activa el simulador XR solo en el editor (nunca en builds) |
| `BuildQuest` | `Assets/Editor/` | 📦 Automatiza el build de Quest desde el menú |
| `CrearAudios3D` | `Assets/Editor/` | 🎵 Crea/configura audios 3D masivamente |
| `HornearLighting` | `Assets/Editor/` | 💡 Hornea la iluminación (bake) de las escenas |
| `ReemplazarRigXR` | `Assets/Editor/` | 🔄 Reemplaza rigs por rigs compatibles con XR |

---

## 🎮 Controles

- 🤲 **Manos**: agarra, sujeta y manipula objetos
- 👣 **Locomoción**: mueve y teletransporta por el minimarket
- 🎯 **Interacción**: apunta, agarra y juega con todo lo que encuentres

---

## 🗺️ Roadmap

- [ ] 🛒 Carrito de compras interactivo
- [ ] 💰 Sistema de caja/escáner
- [ ] 🏷️ Productos con precios y etiquetas
- [ ] 🌙 Modo noche (porque los minimarkets de madrugada dan coisa)
- [ ] 🤖 NPC vendedor
- [ ] 📱 Optimización extra para Quest

---

## 🤝 Contribuir

¿Quieres echar una mano? ¡Bienvenid@! 🙌

1. 🍴 Haz fork del repo
2. 🌿 Crea una rama (`git checkout -b mi-feature`)
3. 💾 Commit con estilo (`git commit -m "agregué algo genial"`)
4. 📤 Push y abre un Pull Request

---

## 📜 Licencia

Distributed under the **MIT License**. Haz lo que quieras, pero crédito siempre se agradece. 😎

---

<p align="center">
  Hecho con ❤️, café ☕ y mucho VR 🥽<br>
  <b>MiniMarket VR © 2026</b>
</p>
