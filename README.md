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

📥 **Download:** [CalcpadSuitePy-Setup-1.0.10.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py/releases) (self-contained, no .NET required)
📁 Ejemplos `.py` bundleados con el installer (se copian a `Documents\Calcpad Suite Py\Examples\`).

> ℹ️ El motor MATLAB (`.m`) vive en el proyecto hermano **Calcpad Lab** — ver
> [`README-CalcpadLab.md`](./README-CalcpadLab.md). Este repo es la variante **Python-only**.

---

## Novedades — Control de auto-render en el motor nativo (v1.0.10, 2026-06-29)

**`#noauto` y `#solografica` ahora también funcionan en el motor Python nativo** (antes solo
en los scripts que corrían sobre Python real). Para scripts "puros" (escalares, `for`/`while`/`if`,
fórmulas, sin imports):

- **`#noauto`** → el reporte deja de auto-renderizar cada variable; solo aparece lo que pidas
  explícitamente: texto (`#'`/`#"`), strings sueltos, `print()`, figuras y lo marcado con `#show`.
- **`#solografica`** → mismo efecto enfocado en dejar solo la gráfica.
- **`#show variable`** muestra una variable concreta **inline**, justo donde la marcás.

Así controlás exactamente qué entra al worksheet sin tener que prefijar todo con `_` para ocultarlo.
Cambios en `Symbolic.Core/Python/PythonPipeline.cs` y `PythonHtmlWriter.cs`.

---

## Novedades — Selector de entornos de Python (2026-06-27)

**Menú `Python` — elegí en qué *entorno* (environment) corren tus scripts**, igual que el
"Select Interpreter" de VS Code. Resuelve el clásico `ModuleNotFoundError: No module named 'numpy'`
al abrir un script en otra PC: ya no dependés del Python global, apuntás Suite Py al entorno que
tiene las librerías. Ver la sección [**Entornos de Python**](#entornos-de-python-environments--dónde-viven-las-librerías).

- **Lista de entornos detectados** automáticamente: Python del sistema (PATH / *py launcher*),
  `venv` (`.venv` / `venv` / `env`) que estén junto al script, venvs agregados a mano, y entornos **conda**.
- **Crear entorno nuevo** desde el menú (`python -m venv`) e **instalar numpy · scipy · matplotlib**
  adentro (pip, con salida en vivo) — sin tocar el Python global.
- **Agregar** un venv existente por carpeta, **verificar** qué librerías tiene, y **abrir** su carpeta.
- La elección **se guarda** (`%LOCALAPPDATA%\CalcpadSuitePy\pyenv.json`) y se reaplica al arrancar.

Código: `Symbolic.Core/Python/PythonEnvironments.cs` (motor) + menú `Python` en `MainWindow.xaml`/`.cs`.

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

1. Descargar **CalcpadSuitePy-Setup-1.0.10.exe** desde los
   [releases del repo](https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py/releases).
2. Doble-click → aceptar UAC → seguir el wizard (acepta asociación `.py` para abrir scripts con doble-click).
3. Al primer arranque, los ejemplos se copian a `Documents\Calcpad Suite Py\Examples\`.
4. Abrir cualquier `.py` (`Ctrl+O`) o crear uno nuevo (`Ctrl+N`); con **AutoRun** se ejecuta al guardar.

**No requiere .NET Desktop Runtime** — el runtime .NET 10 viaja dentro del installer (self-contained).
Para los scripts que **importan librerías** (numpy/scipy/matplotlib) necesitás un **Python** con esas
librerías instaladas; lo elegís desde el menú **`Python`** (ver abajo). Los scripts de Python "puro"
(escalares, `for`/`while`/`if`, fórmulas) **no necesitan instalar nada** — corren en el motor C# nativo.

CLI usage:

```bash
CalcpadSuitePyCli.exe my_script.py html -s   # generate HTML output
CalcpadSuitePyCli.exe my_script.py pdf        # generate PDF
```

## Entornos de Python (environments) — dónde viven las librerías

> **Resumen en una línea:** un *entorno* es una **carpeta** con su propio Python y sus propias
> librerías. En Python **no se instalan las librerías "globalmente"**, se instalan **dentro de un
> entorno** para evitar que dos proyectos se pisen las versiones. Suite Py te deja **elegir** qué
> entorno usar.

### ¿Por qué? (la analogía de la caja de herramientas)

Pensá un entorno como una **caja de herramientas** por proyecto:

- 🧰 **Caja A** (entorno del proyecto estructural) → tiene `numpy`, `scipy`, `matplotlib`.
- 🧰 **Caja B** (otro proyecto) → tiene otras librerías, quizá otra versión de numpy.

Cada caja tiene **solo** lo que ese proyecto necesita. Así, actualizar una librería en un proyecto
**no rompe** otro. Por eso la práctica estándar es **no instalar nada global** y armar un entorno por
proyecto. La doc oficial: <https://docs.python.org/es/3/library/venv.html>.

Un entorno es literalmente una carpeta:

```
mi_proyecto\
├── mesa_torsion.py
└── .venv\                      ← EL ENTORNO (una carpeta)
    ├── pyvenv.cfg
    ├── Scripts\python.exe       ← su Python privado
    └── Lib\site-packages\
        ├── numpy\               ← la librería vive ACÁ, no en el sistema
        ├── scipy\
        └── matplotlib\
```

### ¿Qué es "elegir el entorno"?

Es **decirle a Suite Py de qué carpeta sacar las librerías** al correr un `.py`:

- Elegís un entorno **con** numpy → el script funciona.
- Elegís uno **sin** numpy (típico: el Python "pelado" del sistema) → `ModuleNotFoundError: No module named 'numpy'`.

### Crearlo a mano (terminal) — equivalente a lo que hace el menú

```bat
cd C:\ruta\a\mi_proyecto
python -m venv .venv                       :: crea la carpeta-entorno
.venv\Scripts\activate                     :: "entra" al entorno
pip install numpy scipy matplotlib         :: instala las libs DENTRO del entorno
```

> El mismo `.venv` sirve para **IDLE** (`.venv\Scripts\python.exe -m idlelib`) y para **Suite Py**;
> no hay que instalar las librerías dos veces.

### Hacerlo desde Suite Py (sin terminal) — menú `Python`

| Acción del menú | Qué hace |
|---|---|
| **(lista de entornos)** | Tilda el entorno activo; clic en otro para cambiar. Detecta sistema, `.venv` junto al script y conda. |
| **Agregar entorno existente…** | Apuntá a la carpeta de un `.venv` que ya tengas. |
| **Crear entorno nuevo…** | Corre `python -m venv` y, si querés, instala numpy·scipy·matplotlib enseguida. |
| **Instalar / actualizar numpy·scipy·matplotlib** | `pip install` dentro del entorno activo (salida en vivo). |
| **Verificar librerías** | Muestra ✓/✗ de cada librería en el entorno activo. |
| **Abrir carpeta** | Abre la carpeta del entorno en el Explorador. |

### Indicarlo DENTRO del script — directiva `#venv` / `#env`

Además del menú (elección global, estilo VS Code), el propio `.py` puede **declarar su entorno** con
una directiva en comentario (válida en Python: para IDLE es solo un `#`). Tiene **prioridad** sobre el
menú, **solo para ese script**:

```python
#venv .venv                         # carpeta venv relativa al script  → .venv\Scripts\python.exe
# o:
#env C:\Users\yo\envs\fem           # carpeta venv absoluta
#env C:\Python312\python.exe        # un intérprete concreto, tal cual
import numpy as np                  # sale del entorno declarado arriba
```

Reglas:
- La ruta es **relativa a la carpeta del `.py`** (o absoluta). Puede apuntar a la **carpeta** del venv
  o directo a un `python.exe`.
- Se lee la **primera** directiva `#venv`/`#env` del archivo.
- Si la ruta no resuelve, Suite Py cae a la elección del menú (no rompe).
- Es un comentario normal de Python → el mismo `.py` corre igual en IDLE / VS Code / consola.

> 💡 Útil para que un script "viaje" con su entorno: lo abrís en otra PC y ya sabe qué `.venv` usar,
> sin tocar el menú. Y como Suite Py **auto-detecta** el `.venv` junto al script, muchas veces ni hace
> falta la directiva.

> ⚠️ **Límite de `venv`:** un entorno se "clona" de un Python base, así que la máquina necesita tener
> **algún Python instalado** una vez (de [python.org](https://www.python.org/downloads/)). `venv` aísla
> *las librerías*, no el intérprete base. Para PCs sin ningún Python, la alternativa es empaquetar un
> Python en el instalador (en evaluación).

---

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
