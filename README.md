# Calcpad Suite Py

**Python-syntax scientific worksheets** — same WPF + CLI experience as
[Calcpad](https://calcpad.eu/) but the parser reads pure `.py` files instead
of `.cpd`. Native Python engine in C# **plus** automatic fallback to the
system `python` for `numpy` / `scipy` / `matplotlib` / `plotly`.

> Write standard Python; get a rendered worksheet. Scalar/`for`/`while`/`if`
> code runs on the **native C# engine** (fast, no subprocess); anything that
> imports a library is delegated to the **real Python** of your system.
> Same renderized HTML/PDF/DOCX output as Calcpad, same auto-run-on-save,
> same template — only the input syntax is Python.

📥 **Download:** [CalcpadSuitePy-Setup-1.0.2.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py/releases) (self-contained, no .NET required)
📁 Ejemplos `.py` bundleados con el installer (se copian a `Documents\Calcpad Suite Py\Examples\`).

> ℹ️ El motor MATLAB (`.m`) vive en el proyecto hermano **Calcpad Lab** — ver
> [`README-CalcpadLab.md`](./README-CalcpadLab.md). Este repo es la variante **Python-only**.

---

## Novedades — Calcpad Suite Py v1.0.2 (2026-06-20)

**Scripts de Python REAL (numpy + matplotlib) renderizados como worksheet.** Todo el script
corre en el Python del sistema con sus librerías; el reporte muestra prints, variables y la
**figura de matplotlib embebida** (PNG base64). Mejoras del motor:

- **Output línea por línea (streaming):** el WPF va mostrando cada línea apenas Python la
  imprime, en vez de esperar a que termine todo. (`RealPython.ExecuteStreaming` con `python -u`.)
- **Figuras de matplotlib embebidas:** `plt.show()`/`plt.savefig()` → la figura aparece en el
  reporte (backend Agg + captura de `get_fignums()`). **Animaciones** (`FuncAnimation`) → **GIF** embebido.
- **Imports de módulos hermanos + `open()` relativos:** el subproceso corre con
  `WorkingDirectory` + `PYTHONPATH` en la carpeta del documento → `import mi_modulo` y
  `open("datos.json")` encuentran lo que está junto al `.py`.
- **GUIs (PyQt/PySide/PyVista/tkinter):** se abren en su **ventana nativa** (lanzadas
  desacopladas, sin timeout), en vez de matarse al cerrar.
- **3D interactivo nativo:** `glplot.js`/`GL3` (WebGL propio de Calcpad) inyectado inline →
  3D con **hover datatip** que sigue al cursor, orbitar, zoom y **bandas de contorno ETABS**
  (paleta reverse-engineered de 15 colores), todo en el canvas del WebView2 — **sin three.js, sin CDN**.
- **Mostrar / ocultar variables sin `print`:**
  - modo clásico: las variables se auto-renderizan; prefija con `_` para ocultar una.
  - modo **OPT-IN** (`#noauto` o usar cualquier `#show`): por defecto NO se muestra nada;
    **`#show variable`** la muestra **inline** justo donde la marcas.
  - **`#solografica`** + `plt.show()` → en el reporte sale **solo la gráfica**.
- **`#noprint` (orden global):** Suite-Py **no ejecuta los `print()`** del script; en Python
  real (IDLE) corren normal porque es solo un comentario. (También `#nosuite` al final de una línea.)
- **`#nofig` (orden global):** Suite-Py **no embebe ninguna figura** de matplotlib.
- **Comentarios visibles estilo Calcpad** (válidos en Python): `#'texto` → párrafo, `#"título`
  → encabezado.

Cambios en `Symbolic.Core/Python/` (`PythonPipeline.cs`, `RealPython.cs`, `PythonViz.cs`).
Doc: `calcpad-draw/EQUIVALENCIAS_PYTHON_CalcpadSuitePy.md`.

---

## Why Calcpad Suite Py?

Calcpad oficial es excelente para matemática de ingeniería con su render nativo
de ecuaciones, pero su sintaxis `.cpd` tiene una curva de aprendizaje fuerte para
ingenieros que vienen de Python. **Calcpad Suite Py mantiene todas las fortalezas
visuales de Calcpad (fórmulas renderizadas, auto-run, export PDF/Word, gráficas
inline)** y reemplaza la sintaxis de entrada por **Python estándar**.

Escribes:

```python
#" Datos
a = 6
b = 4
import numpy as np
K = np.array([[a, 1], [1, b]])
f = np.array([10.0, 0.0])
u = np.linalg.solve(K, f)   # se auto-renderiza
print("desplazamientos:", u)
```

…y obtienes un worksheet renderizado (prosa + valores + tablas + gráficas), igual
que Calcpad pero con Python.

- **Motor Python nativo** en C# (`Symbolic.Core/Python/`) — tokenizer + parser +
  evaluator propios para escalares, control de flujo y aritmética (rápido, sin proceso).
- **Fallback automático a python real** cuando el script importa librerías
  (numpy, scipy, sympy, matplotlib, plotly, pyvista…): el script entero corre en el
  `python` del sistema y el resultado (stdout + figuras) se embebe en el reporte.
- **Solver lineal/eigen nativo** (`Symbolic.Core/Native/`): OpenBLAS (`dgesv`, `DGEMM`)
  + Eigen para álgebra densa/dispersa, al nivel de numpy.
- **Gráficas inline** estilo Calcpad y 3D WebGL nativo (`glplot.js`).

---

## Instalación

1. Descargar **CalcpadSuitePy-Setup-1.0.2.exe** desde los
   [releases del repo](https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py/releases).
2. Doble-click → aceptar UAC → seguir el wizard (acepta asociación `.py` para abrir scripts con doble-click).
3. Al primer arranque, los ejemplos se copian a `Documents\Calcpad Suite Py\Examples\`.
4. Abrir cualquier `.py` (`Ctrl+O`) o crear uno nuevo (`Ctrl+N`); con **AutoRun** se ejecuta al guardar.

**No requiere .NET Desktop Runtime** — el runtime .NET 10 viaja dentro del installer (self-contained).
Para el fallback a librerías, necesitas un **Python del sistema** con numpy/matplotlib/scipy instalados.

CLI usage:

```bash
CalcpadSuitePyCli.exe my_script.py html -s   # generate HTML output
CalcpadSuitePyCli.exe my_script.py pdf        # generate PDF
```

## Build from source

Requires **.NET 10 SDK**.

```bash
git clone https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py.git
cd Calcpad-Suite-Py
dotnet build Symbolic.Wpf/Symbolic.Wpf.csproj -c Release
dotnet build Symbolic.Cli/Symbolic.Cli.csproj -c Release
```

---

## Repository structure

```
Symbolic.Core/
├── Python/              ← motor Python nativo (tokenizer + parser + evaluator + viz)
│   ├── PythonTokenizer.cs
│   ├── PythonParser.cs
│   ├── PythonEvaluator.cs
│   ├── PythonHtmlWriter.cs
│   ├── PythonPipeline.cs    ← fachada + fallback a python real
│   └── RealPython.cs        ← puente al `python` del sistema (subprocess)
├── Native/              ← OpenBLAS / Eigen interop (solver denso/disperso)
└── ...                  ← Calcpad-Symbolic core (math + plotting)

Symbolic.Wpf/            ← WPF GUI (CalcpadSuitePy.exe, WebView2 hot reload)
Symbolic.Cli/            ← command-line interface (CalcpadSuitePyCli.exe)

Examples/                ← ejemplos .py (matemáticas, FEM, física, estructural)
```

---

## FEM / validación

Calcpad Suite Py es una de las piezas de validación de
[Hekatan Struct](https://github.com/GiorgioBurbanelli89/hekatan-struct), la
plataforma de análisis estructural en navegador. Los mismos benchmarks se corren
en **varios lenguajes en paralelo** (Python / C++ WASM / API de ETABS-SAP) y deben
dar el mismo resultado:

| Element | vs SAP 2000 / ETABS |
|---|---|
| **Batoz DKQ** vs Plate-Thin (ShellThin/DKE) | match exacto |
| **MITC4** (Dvorkin-Bathe 1985) vs Plate-Thick | -0.56 % deflexión |
| **BFS Q4** (Bogner-Fox-Schmit 1965) | match analítico Navier ~0.1 % |

La idea: si el mismo benchmark da el mismo resultado en todos los lenguajes, la
implementación es correcta. La variante Python es la más cómoda para integrar con
numpy/scipy/notebooks y para reverse-engineering de archivos CSI (e2k / .edb / leyenda de contornos).

---

## Acknowledgments

- Construido sobre [Calcpad Symbolic](https://github.com/Proektsoftbg/Calcpad)
  (Ned Tomov, MIT license) — mismo renderer, mismo motor de math.
- OpenBLAS / Eigen 3 para el álgebra lineal nativa.

## License

MIT
