// =============================================================================
// Calcpad Suite Py — Python Pipeline: fachada Tokenizer + Parser + Evaluator
//                    + HtmlWriter, con fallback al intérprete python real.
// =============================================================================
//   Entry-point único para ejecutar un script Python y obtener HTML.
//   Estrategia (decisión del usuario): lo que el motor C# nativo sabe hacer se
//   ejecuta en C# (rápido, sin proceso externo); lo que no (numpy, sympy,
//   matplotlib, imports no nativos, constructos no soportados) cae al `python`
//   real del sistema (subprocess), reusando el patrón ya existente en Calcpad.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Calcpad.Core.Python
{
    public sealed class PythonPipeline
    {
        private PythonEvaluator _evaluator = new();
        public PyScope GlobalScope => _evaluator.Globals;

        /// <summary>Si true, fuerza el uso del intérprete python real del sistema
        /// (sin intentar el motor nativo). Útil para depurar paridad.</summary>
        public bool ForceRealPython { get; set; }

        /// <summary>Si true (default), permite caer a python real cuando el motor
        /// nativo no soporta algo. Si false, los no-soportados producen error.</summary>
        public bool AllowRealPythonFallback { get; set; } = true;

        public bool StreamingMode { get; set; }
        public event Action<int> StatementStarting;
        public event Action<int, string> StatementCompleted;
        public event Action<string> ScriptFinished;

        /// <summary>Última ejecución usó python real (subprocess).</summary>
        public bool LastRanWithRealPython { get; private set; }

        public string Run(string source)
        {
            LastRanWithRealPython = false;

            List<PyNode> stmts;
            try
            {
                var tokens = PythonTokenizer.Tokenize(source);
                var parser = new PythonParser(tokens);
                stmts = parser.ParseModule();
            }
            catch (Exception) when (ForceRealPython || AllowRealPythonFallback)
            {
                // No parseable nativamente → python real.
                return FinishRealPython(source);
            }

            if (ForceRealPython)
                return FinishRealPython(source);

            // ¿Imports satisfacibles nativamente?
            if (!PythonEvaluator.CanRunNatively(stmts, out var reason))
            {
                if (AllowRealPythonFallback) return FinishRealPython(source);
                return ErrorHtml($"Construcción no soportada por el motor nativo: {reason}");
            }

            try
            {
                return RunNative(stmts);
            }
            catch (PythonNotSupported) when (AllowRealPythonFallback)
            {
                // Algo dinámico no soportado: re-ejecutar todo en python real.
                _evaluator = new PythonEvaluator(); // limpiar estado parcial
                return FinishRealPython(source);
            }
        }

        public (string Html, string Error, int ErrorLine) RunLine(string source, int lineOffset = 0)
        {
            try
            {
                return (Run(source), null, 0);
            }
            catch (PythonParseException pe) { return (null, pe.Message, pe.Line + lineOffset); }
            catch (PythonTokenizeException te) { return (null, te.Message, te.Line + lineOffset); }
            catch (PyRuntimeError re) { return (null, $"{re.PyType}: {re.Message}", lineOffset); }
            catch (Exception ex) { return (null, $"Internal: {ex.GetType().Name}: {ex.Message}", lineOffset); }
        }

        public void Reset() => _evaluator = new PythonEvaluator();

        // ===================================================================
        //  EJECUCIÓN NATIVA
        // ===================================================================
        private string RunNative(List<PyNode> stmts)
        {
            var sb = new StringBuilder();
            var dispBuffer = new StringBuilder();
            _evaluator.Output = msg => dispBuffer.Append(msg);
            _evaluator.HtmlOut = html => sb.Append(html);

            string LineLink(int line) => $"[<a href=\"#0\" data-text=\"{line}\">{line}</a>]";

            int lastLine = -1;
            int pendingStart = sb.Length, pendingLine = -1;

            void FlushDisp(int line)
            {
                if (dispBuffer.Length == 0) return;
                var raw = dispBuffer.ToString();
                dispBuffer.Clear();
                // Procesa marcadores __CPSPY_HTML__ / __CPSPY_IMG__ (HTML/imagen crudos)
                // igual que el fallback, para que print() pueda emitir HTML (ej. visores 3D).
                sb.Append(RenderStdout(raw));
            }

            for (int idx = 0; idx < stmts.Count; idx++)
            {
                var stmt = stmts[idx];

                // Comentario INLINE = directiva del statement anterior (ya consumida) → no renderizar.
                if (stmt is CommentStmt ics && ics.IsInline)
                    continue;

                // ¿Directiva inline para ESTE statement? (comentario en la misma línea, a continuación)
                string directive = null, directiveText = null;
                if (idx + 1 < stmts.Count && stmts[idx + 1] is CommentStmt nc && nc.IsInline)
                    directive = PythonHtmlWriter.CommentKind(nc.Text, out directiveText);

                int stmtLine = stmt?.Line ?? 0;

                if (StreamingMode && pendingLine != -1 && pendingLine != stmtLine
                    && sb.Length > pendingStart && StatementCompleted != null)
                {
                    StatementCompleted.Invoke(pendingLine, sb.ToString(pendingStart, sb.Length - pendingStart));
                    pendingStart = sb.Length;
                }
                pendingLine = stmtLine;
                StatementStarting?.Invoke(stmtLine);

                StatementResult result;
                try
                {
                    result = _evaluator.ExecuteOne(stmt, _evaluator.Globals);
                }
                catch (PythonNotSupported) { throw; } // sube al fallback
                catch (PyRuntimeError ex)
                {
                    FlushDisp(stmtLine);
                    sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">{ex.PyType}: {WebUtility.HtmlEncode(ex.Message)} (line {LineLink(stmtLine)})</p>\n");
                    continue;
                }
                catch (PyRaiseSignal rs)
                {
                    FlushDisp(stmtLine);
                    string m = rs.Value is PyRuntimeError pe ? $"{pe.PyType}: {pe.Message}" : PyOps.Str(rs.Value);
                    sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">{WebUtility.HtmlEncode(m)} (line {LineLink(stmtLine)})</p>\n");
                    continue;
                }

                // Volcar print() antes del render del statement.
                FlushDisp(stmtLine);

                // #hide → se ejecuta (ya se ejecutó) pero NO se muestra nada.
                if (result.Display && directive != "hide")
                {
                    try
                    {
                        var html = PythonHtmlWriter.RenderStatement(stmt, result, directive, directiveText);
                        if (!string.IsNullOrEmpty(html))
                        {
                            sb.Append($"<p class=\"line\" id=\"line-{stmtLine}\">");
                            sb.Append(html);
                            sb.Append("</p>\n");
                        }
                    }
                    catch (PythonNotSupported) { throw; }
                    catch (Exception rex)
                    {
                        sb.Append($"<p class=\"err\" id=\"line-{stmtLine}\">Render error: {WebUtility.HtmlEncode(rex.Message)}</p>\n");
                    }
                }
                lastLine = stmtLine;
            }

            // Flush final de print pendiente.
            FlushDisp(lastLine < 0 ? 0 : lastLine);

            if (StreamingMode && pendingLine != -1 && sb.Length > pendingStart && StatementCompleted != null)
                StatementCompleted.Invoke(pendingLine, sb.ToString(pendingStart, sb.Length - pendingStart));

            var full = sb.ToString();
            ScriptFinished?.Invoke(full);
            return full;
        }

        // ===================================================================
        //  FALLBACK: PYTHON REAL (subprocess)
        // ===================================================================
        // Detecta scripts de INTERFAZ GRÁFICA (PyQt5/6, PySide2/6, PyVista, tkinter mainloop).
        // Estos abren su propia ventana nativa y bloquean en exec_()/mainloop(): se lanzan
        // DESACOPLADOS (sin timeout, sin capturar stdout) para que la interfaz se vea, igual
        // que al correr `python script.py` a mano.
        private static bool IsGuiScript(string source)
        {
            // OJO: 'pyvista' (a secas) NO va aquí — se intercepta su show() y se EMBEBE
            // como PNG (ver _pvPreamble). Solo los full-app Qt/tk/visores propios se detachan.
            string[] markers = {
                "PyQt5", "PyQt6", "PySide2", "PySide6", "pyvistaqt", "QtInteractor",
                ".exec_()", ".exec()", ".mainloop(", "QApplication", "vedo",
                "mayavi", "glfw", "pyglet"
            };
            foreach (var m in markers)
                if (source.Contains(m, StringComparison.Ordinal)) return true;
            return false;
        }

        private string FinishRealPython(string source)
        {
            LastRanWithRealPython = true;

            // ── INTERFAZ GRÁFICA (Qt/PyVista/tk): abrir ventana nativa desacoplada ──
            if (IsGuiScript(source))
            {
                var (info, okGui) = RealPython.ExecuteDetached(source);
                var cls = okGui ? "line" : "err";
                var html = $"<p class=\"{cls}\"><span class=\"eq\"><span style=\"white-space:pre-wrap\">" +
                           WebUtility.HtmlEncode(info) + "</span></span></p>\n";
                ScriptFinished?.Invoke(html);
                if (StreamingMode && StatementCompleted != null) StatementCompleted.Invoke(0, html);
                return html;
            }

            var instrumented = InstrumentForRender(source);

            // STREAMING (WPF): el subproceso de Python transmite su stdout LÍNEA POR LÍNEA
            // y cada una se manda al WebView2 apenas llega → output progresivo, no todo de golpe.
            if (StreamingMode && StatementCompleted != null)
            {
                var acc = new StringBuilder();
                var (stderrS, okS) = RealPython.ExecuteStreaming(instrumented, ln =>
                {
                    var chunk = RenderStdoutLine(ln);
                    if (chunk.Length == 0) return;
                    acc.Append(chunk);
                    StatementCompleted.Invoke(0, chunk);   // ← chunk vivo al panel de output
                });
                if (!okS && !string.IsNullOrEmpty(stderrS))
                {
                    var errHtml = "<p class=\"err\"><span style=\"white-space:pre-wrap\">" +
                        WebUtility.HtmlEncode(stderrS.TrimEnd('\n')) + "</span></p>\n";
                    acc.Append(errHtml);
                    StatementCompleted.Invoke(0, errHtml);
                }
                var fullS = acc.Length > 0 ? acc.ToString()
                    : "<p class=\"line\"><span class=\"eq\"><i>(sin salida)</i></span></p>\n";
                ScriptFinished?.Invoke(fullS);
                return fullS;
            }

            // BATCH (CLI / no streaming): todo de una.
            var (stdout, stderr, ok) = RealPython.Execute(instrumented);
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(stdout))
                sb.Append(RenderStdout(stdout));
            if (!ok && !string.IsNullOrEmpty(stderr))
            {
                sb.Append("<p class=\"err\"><span style=\"white-space:pre-wrap\">");
                sb.Append(WebUtility.HtmlEncode(stderr.TrimEnd('\n')));
                sb.Append("</span></p>\n");
            }
            if (sb.Length == 0)
                sb.Append("<p class=\"line\"><span class=\"eq\"><i>(sin salida)</i></span></p>\n");
            var full = sb.ToString();
            ScriptFinished?.Invoke(full);
            return full;
        }

        /// <summary>Renderiza UNA línea de stdout de python (para streaming): __CPSPY_IMG__→&lt;img&gt;,
        /// __CPSPY_HTML__→HTML crudo, resto→texto pre-wrap. Líneas en blanco → "".</summary>
        private static string RenderStdoutLine(string ln)
        {
            if (ln.StartsWith(ImgMarker, StringComparison.Ordinal))
            {
                var b64 = ln.Substring(ImgMarker.Length).Trim();
                return b64.Length == 0 ? "" : ImgTag(b64, "png");
            }
            if (ln.StartsWith(GifMarker, StringComparison.Ordinal))
            {
                var b64 = ln.Substring(GifMarker.Length).Trim();
                return b64.Length == 0 ? "" : ImgTag(b64, "gif");
            }
            if (ln.StartsWith(HtmlMarker, StringComparison.Ordinal))
                return ln.Substring(HtmlMarker.Length) + "\n";
            if (ln.TrimEnd().Length == 0) return "";
            return "<p class=\"line\"><span class=\"eq\"><span style=\"white-space:pre;font-family:Consolas,'Courier New',monospace\">" +
                WebUtility.HtmlEncode(ln) + "</span></span></p>\n";
        }

        // Marcadores que el script Python puede imprimir para enriquecer el reporte:
        //   print("__CPSPY_IMG__:"  + base64png)   → imagen PNG embebida
        //   print("__CPSPY_HTML__:" + htmlCrudo)    → HTML con markup Calcpad
        //                                             (.eq, var, .mat, <table>...) SIN escapar
        // Así los valores/tablas se ven como worksheet Calcpad (no texto plano).
        private const string ImgMarker = "__CPSPY_IMG__:";
        private const string GifMarker = "__CPSPY_GIF__:";   // animación → GIF embebido
        private const string HtmlMarker = "__CPSPY_HTML__:";

        /// <summary>&lt;img&gt; centrado con data-uri base64 del tipo dado (png/gif).</summary>
        private static string ImgTag(string b64, string kind) =>
            "<p class=\"line\" style=\"text-align:center\"><img src=\"data:image/" + kind +
            ";base64," + b64 + "\" style=\"max-width:100%;height:auto\"/></p>\n";

        /// <summary>Renderiza el stdout de python: texto plano como bloque pre-wrap,
        /// __CPSPY_IMG__ como &lt;img&gt;, y __CPSPY_HTML__ como HTML crudo (markup Calcpad).</summary>
        private static string RenderStdout(string stdout)
        {
            var sb = new StringBuilder();
            var lines = stdout.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var text = new StringBuilder();
            void FlushText()
            {
                if (text.Length == 0) return;
                var t = text.ToString().TrimEnd('\n');
                if (t.Length > 0)
                    sb.Append("<p class=\"line\"><span class=\"eq\"><span style=\"white-space:pre;font-family:Consolas,'Courier New',monospace\">")
                      .Append(WebUtility.HtmlEncode(t))
                      .Append("</span></span></p>\n");
                text.Clear();
            }
            foreach (var ln in lines)
            {
                if (ln.StartsWith(ImgMarker, StringComparison.Ordinal))
                {
                    FlushText();
                    var b64 = ln.Substring(ImgMarker.Length).Trim();
                    if (b64.Length > 0) sb.Append(ImgTag(b64, "png"));
                }
                else if (ln.StartsWith(GifMarker, StringComparison.Ordinal))
                {
                    FlushText();
                    var b64 = ln.Substring(GifMarker.Length).Trim();
                    if (b64.Length > 0) sb.Append(ImgTag(b64, "gif"));
                }
                else if (ln.StartsWith(HtmlMarker, StringComparison.Ordinal))
                {
                    FlushText();
                    sb.Append(ln.Substring(HtmlMarker.Length)).Append('\n');  // HTML crudo (markup Calcpad)
                }
                else
                {
                    text.Append(ln).Append('\n');
                }
            }
            FlushText();
            return sb.ToString();
        }

        private static string ErrorHtml(string msg) =>
            $"<p class=\"err\">{WebUtility.HtmlEncode(msg)}</p>\n";

        // ===================================================================
        //  INSTRUMENTACIÓN PARA RENDER EN EL FALLBACK (python real)
        //  El motor nativo auto-renderiza cada asignación; el python real solo
        //  da stdout. Para que las variables se VEAN (como worksheet Calcpad),
        //  añadimos un footer que vuelca los nombres top-level como markup
        //  via el marcador __CPSPY_HTML__ (lo recoge RenderStdout).
        // ===================================================================
        private static readonly HashSet<string> _pyKw = new()
        {
            "if","elif","else","for","while","def","class","return","import","from","as",
            "in","not","and","or","None","True","False","pass","break","continue","with",
            "try","except","finally","raise","lambda","global","nonlocal","del","assert",
            "yield","print","async","await"
        };

        private static string InstrumentForRender(string source)
        {
            var names = new List<string>();
            var seen = new HashSet<string>();
            foreach (var raw in source.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0 || char.IsWhiteSpace(raw[0])) continue; // indentado/blank → no top-level
                var m = System.Text.RegularExpressions.Regex.Match(
                    raw, @"^([A-Za-z_][A-Za-z0-9_]*)\s*(?::[^=]+)?=(?!=)");
                if (!m.Success) continue;
                var n = m.Groups[1].Value;
                if (_pyKw.Contains(n)) continue;
                if (n.StartsWith("_")) continue;   // privados/internos → no volcar
                if (seen.Add(n)) names.Add(n);
            }
            bool usesMpl = source.Contains("matplotlib");
            bool usesPv = source.Contains("pyvista") && !source.Contains("pyvistaqt") && !source.Contains("QtInteractor");
            bool hasVisibleComments = source.Contains("#'") || source.Contains("#\"");
            // #noauto / #solografica → NO auto-renderizar variables (solo prints/figuras explícitos,
            // como en Python real). Son comentarios válidos de Python (no rompen).
            // #show <var> en cualquier línea → modo OPT-IN: por defecto NO se auto-renderiza
            // nada; SOLO lo marcado con #show se muestra (inline). #noauto/#solografica también
            // activan opt-in (sin mostrar nada salvo prints/figuras explícitos).
            bool hasShow = System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)^\s*#show\s+[A-Za-z_]");
            bool noAuto = hasShow || source.Contains("#noauto") || source.Contains("#solografica");
            // #noprint → Suite-Py NO ejecuta los print() del usuario (en Python real SÍ corren,
            // porque #noprint es solo un comentario). Asi no hay que comentar cada print.
            bool noPrint = source.Contains("#noprint");
            // #nofig → Suite-Py NO embebe NINGUNA figura de matplotlib (en Python real igual abren ventana).
            bool noFig = usesMpl && source.Contains("#nofig");
            bool needsTransform = hasVisibleComments || hasShow || noPrint || source.Contains("#nosuite");
            if ((names.Count == 0 || noAuto) && !usesMpl && !usesPv && !needsTransform) return source;
            var sb = new StringBuilder();
            sb.Append("_realprint = print\n");      // print REAL para uso interno (markers/figuras)
            if (usesMpl) sb.Append(_mplPreamble);   // captura figuras matplotlib (Agg + patch show)
            if (usesPv) sb.Append(_pvPreamble);     // captura PyVista (Plotter.show → screenshot off-screen)
            sb.Append(_pyHelpers);                  // defs de _cpspy_* ANTES (para #show inline)
            if (noPrint) sb.Append("print = lambda *a, **k: None  # #noprint: Suite-Py omite prints del usuario\n");
            if (noFig) sb.Append("_cpspy_flush_figs = lambda: None  # #nofig: Suite-Py no embebe figuras\n");
            sb.Append(TransformVisibleComments(source));   // #'/#" texto + #show <var> inline + #nosuite
            if (!noAuto)                            // modo clásico: volcar TODAS las variables al final
                foreach (var n in names)
                    sb.Append("try:\n    _cpspy_emit('").Append(n).Append("', ").Append(n)
                      .Append(")\nexcept Exception:\n    pass\n");
            if (usesMpl) sb.Append("try:\n    _cpspy_flush_figs()\nexcept Exception:\n    pass\n");
            return sb.ToString();
        }

        // Transforma comentarios VISIBLES (estilo Ned) en prints que emiten HTML, para que
        // se vean también en scripts de Python REAL (no solo en el motor nativo):
        //   #'texto  → párrafo visible        #"titulo → encabezado h3
        // El resto de comentarios (# ...) quedan invisibles, como en Python.
        private static string TransformVisibleComments(string source)
        {
            var lines = source.Replace("\r\n", "\n").Split('\n');
            var outp = new StringBuilder();
            foreach (var raw in lines)
            {
                int i = 0;
                while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t')) i++;
                string rest = raw.Substring(i);
                // línea de CÓDIGO con marcador trailing #nosuite → NO se ejecuta en Suite-Py
                // (se vuelve 'pass'); en Python real (IDLE) la línea corre normal porque el
                // #nosuite es solo un comentario al final. Ideal para prints de depuración.
                if (!rest.StartsWith("#") && rest.Contains("#nosuite"))
                {
                    outp.Append(raw.Substring(0, i)).Append("pass\n");
                    continue;
                }
                // #show <variable> → mostrar esa variable INLINE en este punto (opt-in)
                if (rest.StartsWith("#show"))
                {
                    string after = rest.Length > 5 ? rest.Substring(5).Trim() : "";
                    if (after.Length > 0 &&
                        System.Text.RegularExpressions.Regex.IsMatch(after, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                    {
                        string ind = raw.Substring(0, i);
                        outp.Append(ind).Append("try:\n")
                            .Append(ind).Append("    _cpspy_emit('").Append(after).Append("', ").Append(after).Append(")\n")
                            .Append(ind).Append("except Exception:\n")
                            .Append(ind).Append("    pass\n");
                        continue;
                    }
                    outp.Append(raw).Append('\n');   // "#show" sin nombre → comentario normal
                    continue;
                }
                if (rest.StartsWith("#'") || rest.StartsWith("#\""))
                {
                    bool heading = rest[1] == '"';
                    string content = rest.Substring(2).Trim();
                    if (content.Length == 0) { outp.Append('\n'); continue; }
                    string enc = WebUtility.HtmlEncode(content).Replace("'", "&#39;");
                    string html = heading
                        ? "<h3>" + enc + "</h3>"
                        : "<p class=\"line\"><span class=\"eq\"><span style=\"white-space:pre-wrap\">" + enc + "</span></span></p>";
                    string pyStr = html.Replace("\\", "\\\\");   // (enc ya no tiene comillas simples)
                    outp.Append(raw.Substring(0, i))             // conservar indentación
                        .Append("_realprint('__CPSPY_HTML__:").Append(pyStr).Append("')\n");
                }
                else outp.Append(raw).Append('\n');
            }
            return outp.ToString();
        }

        // Captura de figuras matplotlib: backend Agg + plt.show() -> PNG base64 (__CPSPY_IMG__).
        // Asi el .py es Python PURO (plt.show()) y la figura aparece embebida en Calcpad-Py;
        // ejecutado con python real, plt.show() abre la ventana normal.
        //
        // ANIMACIONES: FuncAnimation/ArtistAnimation se interceptan (se registran al crearse)
        // y al flush se guardan como GIF (PillowWriter) embebido via __CPSPY_GIF__. La figura
        // de cada animación NO se vuelca también como PNG estático (se evita el duplicado).
        // Si Pillow no está, cae a guardar el primer frame como PNG.
        private const string _mplPreamble = @"
try:
    import matplotlib as _mpl
    _mpl.use(""Agg"")
    import matplotlib.pyplot as _pltcap
    import matplotlib.animation as _animcap
    import io as _iocap, base64 as _b64cap, os as _oscap, tempfile as _tmpcap
    _cpspy_anims = []
    def _cpspy_track_anim(_an):
        _cpspy_anims.append(_an); return _an
    _Orig_FuncAnim = _animcap.FuncAnimation
    class _CpspyFuncAnimation(_Orig_FuncAnim):
        def __init__(self, *a, **k):
            super().__init__(*a, **k); _cpspy_track_anim(self)
    _animcap.FuncAnimation = _CpspyFuncAnimation
    _pltcap.matplotlib.animation.FuncAnimation = _CpspyFuncAnimation
    _Orig_ArtistAnim = _animcap.ArtistAnimation
    class _CpspyArtistAnimation(_Orig_ArtistAnim):
        def __init__(self, *a, **k):
            super().__init__(*a, **k); _cpspy_track_anim(self)
    _animcap.ArtistAnimation = _CpspyArtistAnimation
    def _cpspy_anim_fps(_an):
        _iv = getattr(_an, '_interval', None)
        try: return max(1, round(1000.0 / float(_iv))) if _iv else 20
        except Exception: return 20
    def _cpspy_flush_figs():
        _anim_figs = set()
        for _an in _cpspy_anims:
            _fig = getattr(_an, '_fig', None)
            _gp = None
            try:
                # PillowWriter exige una RUTA de archivo (no acepta BytesIO) → temp .gif
                _fd, _gp = _tmpcap.mkstemp(suffix="".gif"", prefix=""cpspy_anim_""); _oscap.close(_fd)
                _an.save(_gp, writer=_animcap.PillowWriter(fps=_cpspy_anim_fps(_an)))
                with open(_gp, ""rb"") as _gf:
                    _realprint(""__CPSPY_GIF__:"" + _b64cap.b64encode(_gf.read()).decode())
                if _fig is not None: _anim_figs.add(id(_fig))
            except Exception:
                pass  # sin Pillow → la figura cae como PNG estático abajo
            finally:
                try:
                    if _gp and _oscap.path.exists(_gp): _oscap.remove(_gp)
                except Exception: pass
        _cpspy_anims.clear()
        for _n in _pltcap.get_fignums():
            _f = _pltcap.figure(_n)
            if id(_f) in _anim_figs: continue
            _bf = _iocap.BytesIO(); _f.savefig(_bf, format=""png"", dpi=110, bbox_inches=""tight""); _bf.seek(0)
            _realprint(""__CPSPY_IMG__:"" + _b64cap.b64encode(_bf.read()).decode())
        _pltcap.close(""all"")
    _pltcap.show = lambda *a, **k: _cpspy_flush_figs()
except Exception:
    def _cpspy_flush_figs(): pass
";

        // Captura de PyVista: patchea Plotter.show() → convierte las mallas al motor 3D
        // nativo GL3 (glplot.js) → 3D INTERACTIVO embebido (rotar + hover + bandas ETABS),
        // misma visualización que PyVista pero dentro del worksheet (sin ventana externa).
        // Si la malla es enorme (>40k triángulos) cae a screenshot PNG estático.
        private const string _pvPreamble = @"
try:
    import pyvista as _pvcap
    _pvcap.OFF_SCREEN = True
    import numpy as _nppv, os as _ospv, tempfile as _tmppv, io as _iopv, base64 as _b64pv
    def _cpspy_pv_tris(tm):
        fa = getattr(tm, 'faces', None); out = []
        if fa is not None and len(fa):
            fa = _nppv.asarray(fa); i = 0
            while i < len(fa):
                n = int(fa[i])
                if n == 3: out.append((int(fa[i+1]), int(fa[i+2]), int(fa[i+3])))
                elif n == 4: out.append((int(fa[i+1]), int(fa[i+2]), int(fa[i+3]))); out.append((int(fa[i+1]), int(fa[i+3]), int(fa[i+4])))
                i += n + 1
        return out
    def _cpspy_pv_show(self, *a, **k):
        try:
            meshes = list(getattr(self, 'meshes', []) or [])
            smin = smax = None
            for m in meshes:
                sc = getattr(m, 'active_scalars', None)
                if sc is not None and len(sc) and _nppv.asarray(sc).ndim == 1:
                    lo = float(_nppv.nanmin(sc)); hi = float(_nppv.nanmax(sc))
                    smin = lo if smin is None else min(smin, lo); smax = hi if smax is None else max(smax, hi)
            if smin is None: smin, smax = 0.0, 1.0
            rng = (smax - smin) or 1.0
            calls = []; dps = []; lo3 = [1e30,1e30,1e30]; hi3 = [-1e30,-1e30,-1e30]; ntri = 0
            for m in meshes:
                try: tm = m.triangulate()
                except Exception: tm = m
                pts = _nppv.asarray(tm.points, float)
                if len(pts) == 0: continue
                for d in range(3): lo3[d] = min(lo3[d], float(pts[:,d].min())); hi3[d] = max(hi3[d], float(pts[:,d].max()))
                sc = getattr(tm, 'active_scalars', None)
                sc = _nppv.asarray(sc).ravel() if (sc is not None and len(sc)) else None
                if len(dps) < 6000:                          # hover: registrar nudos+valor (datatip)
                    _st = max(1, len(pts)//6000)
                    for _ii in range(0, len(pts), _st):
                        _v = float(sc[_ii]) if sc is not None else 0.0
                        dps.append('GL3.datapoint(%.4f,%.4f,%.4f,%.6g);' % (pts[_ii,0],pts[_ii,1],pts[_ii,2],_v))
                for (a0,b0,c0) in _cpspy_pv_tris(tm):
                    p0,p1,p2 = pts[a0],pts[b0],pts[c0]
                    if sc is not None: t0=(float(sc[a0])-smin)/rng; t1=(float(sc[b0])-smin)/rng; t2=(float(sc[c0])-smin)/rng
                    else: t0=t1=t2=0.5
                    arr = '[' + ','.join('%.4f'%v for v in (p0[0],p0[1],p0[2],p1[0],p1[1],p1[2],p2[0],p2[1],p2[2],p2[0],p2[1],p2[2])) + ']'
                    calls.append('GL3.fill3(%s,%.4f,%.4f,%.4f,%.4f);' % (arr,t0,t1,t2,t2)); ntri += 1
            if ntri == 0 or ntri > 40000:
                from PIL import Image as _Impv
                _img = self.screenshot(return_img=True); _bf = _iopv.BytesIO()
                _Impv.fromarray(_img).save(_bf, format='PNG'); _bf.seek(0)
                _realprint('__CPSPY_IMG__:' + _b64pv.b64encode(_bf.read()).decode()); return
            _gljs = open(_ospv.path.join(_tmppv.gettempdir(),'cpspy_glplot.min.js'), encoding='utf-8').read()
            js = ('GL3.figure3(""pv"",860,620);GL3.etabs=true;GL3.view3(40,20);'
                + 'GL3.axis3(%.4f,%.4f,%.4f,%.4f,%.4f,%.4f);' % (lo3[0],hi3[0],lo3[1],hi3[1],lo3[2],hi3[2])
                + ''.join(calls) + 'GL3.datatip(""valor"");' + ''.join(dps)
                + 'GL3.render3();GL3.colorbar3(%.4f,%.4f,340);' % (smin,smax))
            html = '<div><script>if(!window.GL3){' + _gljs + '}</script><script>(function(){' + js + '})();</script></div>'
            _realprint('__CPSPY_HTML__:' + html.replace(chr(10),' ').replace(chr(13),' '))
        except Exception as _e:
            _realprint('__CPSPY_HTML__:<p class=""err"">PyVista 3D: ' + str(_e) + '</p>')
    _pvcap.Plotter.show = _cpspy_pv_show
except Exception:
    pass
";

        private const string _pyHelpers = @"
import html as _cpspy_html
def _cpspy_n(x):
    if isinstance(x, float): return ('%.6g' % x)
    s = str(x)
    if len(s) > 120: s = s[:120] + '…'           # truncar celdas/escalares largos
    return _cpspy_html.escape(s)
def _cpspy_fmt(v):
    try:
        import numpy as _np
        if isinstance(v, _np.ndarray):
            if v.size > 200: return _cpspy_html.escape('ndarray shape=' + str(v.shape) + ' dtype=' + str(v.dtype))
            v = v.tolist()
    except Exception:
        pass
    if isinstance(v, dict):                        # dicts (ej. resultados FEM): solo las claves, compacto
        ks = list(v.keys())
        return _cpspy_html.escape('{' + ', '.join(str(k) for k in ks[:20]) + ('' if len(ks)<=20 else ', …') + '}')
    if isinstance(v, (list, tuple)):
        flat = (len(v) > 0 and isinstance(v[0], (list, tuple)))
        n = min(len(v), 200)                       # limitar filas para no volcar matrices enormes
        rows = ''
        if flat:
            for r in v[:n]:
                rows += '<span class=""row"">' + ''.join('<span class=""cell"">' + _cpspy_n(c) + '</span>' for c in r[:50]) + '</span>'
        else:
            rows = '<span class=""row"">' + ''.join('<span class=""cell"">' + _cpspy_n(x) + '</span>' for x in v[:200]) + '</span>'
        return '<span class=""mat""><span class=""lb""></span><span class=""cells"">' + rows + '</span><span class=""rb""></span></span>'
    return _cpspy_n(v)
def _cpspy_emit(name, val):
    import types as _t
    if callable(val) or isinstance(val, _t.ModuleType): return
    _h = '<p class=""line""><span class=""eq""><var>' + name + '</var> = ' + _cpspy_fmt(val) + '</span></p>'
    _realprint('__CPSPY_HTML__:' + _h.replace(chr(10),' ').replace(chr(13),' '))   # marcador es POR-LINEA → 1 sola linea
";
    }
}
