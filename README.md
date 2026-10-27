<div align="center">

<img src="docs/img/pasillo.jpg" alt="Pasillo del minimarket lleno de productos" width="100%">

<br>

# 🛒 Cajero XR

### Aprende a ser cajero de un minimarket… en realidad virtual.

Escanea productos, cobra en efectivo o con tarjeta, devuelve el vuelto exacto<br>
y atiende a clientes que hacen fila, pagan y se quejan si te equivocas.

<br>

![Unity](https://img.shields.io/badge/Unity-6000.4.9f1-111111?style=for-the-badge&logo=unity&logoColor=white)
![Meta Quest](https://img.shields.io/badge/Meta_Quest-APK-0467DF?style=for-the-badge&logo=meta&logoColor=white)
![PC](https://img.shields.io/badge/PC-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![OpenXR](https://img.shields.io/badge/OpenXR-1.16.1-5586A4?style=for-the-badge)
![Licencia](https://img.shields.io/badge/Licencia-MIT-22C55E?style=for-the-badge)

<br>

[**Qué es**](#-qué-es) &nbsp;·&nbsp;
[**Cómo se cobra**](#-cómo-se-cobra) &nbsp;·&nbsp;
[**Capturas**](#-capturas) &nbsp;·&nbsp;
[**Controles**](#-controles) &nbsp;·&nbsp;
[**Instalación**](#-instalación) &nbsp;·&nbsp;
[**Herramientas**](#-herramientas-del-editor) &nbsp;·&nbsp;
[**Roadmap**](#-roadmap)

</div>

<br>

## ✨ Qué es

**Cajero XR** es un simulador en realidad virtual hecho en **Unity 6** para **practicar el trabajo de cajero** sin presión: escaneas, cobras y das vuelto como en una tienda de verdad, con clientes que llegan solos y te dicen cuando algo salió mal. También se puede jugar solo por diversión.

<table>
<tr>
<td width="33%" valign="top">

### 🏪 Tienda llena
Más de **5 000 productos** con su **tamaño real**, todos se pueden agarrar y escanear. Se dibujan por bloques para que el Quest aguante.

</td>
<td width="33%" valign="top">

### 🧾 Caja con pasos
Escaneo con pistola, **efectivo o tarjeta (POS)**, billetes y monedas de **Perú** y **vuelto verificado** por la caja.

</td>
<td width="33%" valign="top">

### 🧑‍🤝‍🧑 Clientes reales
Entran, eligen productos, **hacen fila**, te dicen cómo pagan y **reclaman** si les cobras de más o te equivocas.

</td>
</tr>
<tr>
<td valign="top">

### 💲 Base de datos
**90 productos** con código de barras, categoría, **precio en soles** y stock, en un JSON fácil de editar.

</td>
<td valign="top">

### 🤲 Manos y mandos
Agarra con los **controladores** o con tus **manos** (XR Hands). Camina con joystick o **moviendo los brazos**.

</td>
<td valign="top">

### 🌙 Día y noche
La tienda pasa del día a la noche; la **venta en curso y el turno se conservan** al cambiar.

</td>
</tr>
</table>


<br>

## 🧾 Cómo se cobra

Arriba de la pantalla de la caja siempre dice **en qué paso estás**. El orden es este:

```mermaid
flowchart LR
    A["🔫 1. Escanear<br/>productos"] --> B{"💬 2. ¿Cómo paga<br/>el cliente?"}
    B -- "💵 Efectivo" --> C["3. Marcar lo que<br/>te dio el cliente"]
    C --> D["4. Devolver el<br/>vuelto exacto"]
    B -- "💳 Tarjeta" --> E["3. Enviar al POS<br/>y esperar APROBADO"]
    D --> F(["✅ Venta completada"])
    E --> F
    F --> G["Siguiente cliente"]
```

| Paso | Qué haces | Botones |
|:---:|---|---|
| **1** | Acerca la pistola a cada producto y aprieta el **gatillo**. Cada bip suma 1 unidad. | **−1 · +1 · Quitar · Cancelar venta** para corregir. Luego **COBRAR ›** |
| **2** | Mira la burbuja del cliente: *"Con tarjeta, por favor"* o *"En efectivo. Aquí tienes S/ 50"*. | **EFECTIVO** o **TARJETA** |
| **3** 💵 | Marca los billetes y monedas que te dio. | **S/ 200 … S/ 0.10 · Exacto · Borrar** → **CONFIRMAR ›** |
| **4** 💵 | Marca el vuelto que le devuelves. La caja revisa que sea **exacto**. | Billetes y monedas · **Ayuda** · **Borrar** → **ENTREGAR VUELTO** |
| **3** 💳 | El cliente acerca la tarjeta: *Procesando… → APROBADO*. | **ENVIAR AL POS** |
| ✅ | El cliente se despide (o reclama si algo salió mal). | **Siguiente cliente** |

> [!TIP]
> Si no sabes cómo armar el vuelto, el botón **Ayuda** te sugiere qué billetes y monedas dar.<br>
> Ejemplo: vuelto de **S/ 23.60** → 1 × S/ 20 · 1 × S/ 2 · 1 × S/ 1 · 1 × S/ 0.50 · 1 × S/ 0.10.


<br>

## 📸 Capturas

<table>
<tr>
<td width="50%"><img src="docs/img/pantalla.jpg" alt="Pantalla de la caja con la venta completada"></td>
<td width="50%"><img src="docs/img/caja.jpg" alt="Cliente esperando en la caja con sus productos en la banda"></td>
</tr>
<tr>
<td align="center"><sub><b>Pantalla de la caja</b> · venta completada y vuelto entregado</sub></td>
<td align="center"><sub><b>Vista del cajero</b> · el cliente deja sus productos en la banda</sub></td>
</tr>
<tr>
<td width="50%"><img src="docs/img/burbuja.jpg" alt="Cliente saludando con una burbuja de diálogo"></td>
<td width="50%"><img src="docs/img/cliente.jpg" alt="Cliente caminando por el pasillo"></td>
</tr>
<tr>
<td align="center"><sub><b>Clientes que hablan</b> · saludan, dicen cómo pagan y reclaman</sub></td>
<td align="center"><sub><b>Pasillos llenos</b> · productos de tamaño real</sub></td>
</tr>
</table>
