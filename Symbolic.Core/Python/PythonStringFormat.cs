// =============================================================================
// Calcpad Suite Py — Formato de strings estilo Python
// =============================================================================
//   - Format spec mini-lenguaje: [[fill]align][sign][#][0][width][,][.prec][type]
//     usado por f-strings ({x:.3f}) y por str.format / format().
//   - Operador % (printf-style): "%.2f" % x, "%s=%d" % (a, b).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Calcpad.Core.Python
{
    public static class PyStringFormat
    {
        // ── Format spec (PEP 3101) ──
        public static string FormatSpec(object value, string spec)
        {
            if (string.IsNullOrEmpty(spec)) return PyOps.Str(value);

            // Parse: [[fill]align][sign][#][0][width][,][.prec][type]
            int i = 0; int n = spec.Length;
            char fill = ' '; char align = '\0';
            if (n >= 2 && (spec[1] == '<' || spec[1] == '>' || spec[1] == '^' || spec[1] == '='))
            { fill = spec[0]; align = spec[1]; i = 2; }
            else if (n >= 1 && (spec[0] == '<' || spec[0] == '>' || spec[0] == '^' || spec[0] == '='))
            { align = spec[0]; i = 1; }

            char sign = '\0';
            if (i < n && (spec[i] == '+' || spec[i] == '-' || spec[i] == ' ')) { sign = spec[i]; i++; }
            bool alt = false;
            if (i < n && spec[i] == '#') { alt = true; i++; }
            bool zeroPad = false;
            if (i < n && spec[i] == '0') { zeroPad = true; if (align == '\0') { align = '='; fill = '0'; } i++; }
            int width = 0;
            while (i < n && char.IsDigit(spec[i])) { width = width * 10 + (spec[i] - '0'); i++; }
            bool comma = false;
            if (i < n && (spec[i] == ',' || spec[i] == '_')) { comma = true; i++; }
            int prec = -1;
            if (i < n && spec[i] == '.')
            {
                i++; prec = 0;
                while (i < n && char.IsDigit(spec[i])) { prec = prec * 10 + (spec[i] - '0'); i++; }
            }
            char type = '\0';
            if (i < n) type = spec[i];

            string body = FormatBody(value, type, prec, alt, comma, sign);

            // Aplicar ancho/relleno
            if (body.Length < width)
            {
                int pad = width - body.Length;
                switch (align)
                {
                    case '<': body = body + new string(fill, pad); break;
                    case '^': body = new string(fill, pad / 2) + body + new string(fill, pad - pad / 2); break;
                    case '=':
                        // relleno entre signo y dígitos
                        if (body.Length > 0 && (body[0] == '-' || body[0] == '+' || body[0] == ' '))
                            body = body[0] + new string(fill, pad) + body.Substring(1);
                        else body = new string(fill, pad) + body;
                        break;
                    default:
                        bool numeric = PyOps.IsNumber(value);
                        if (numeric && align == '\0') body = new string(fill, pad) + body; // números: derecha
                        else if (align == '>') body = new string(fill, pad) + body;
                        else body = new string(fill, pad) + body; // default right para números
                        if (!numeric && align == '\0') body = body.Substring(0, body.Length - pad).PadRight(width); // strings izquierda
                        break;
                }
                if (align == '\0' && !PyOps.IsNumber(value))
                {
                    // strings alinean a la izquierda por defecto
                    body = body.TrimEnd();
                    body = body.PadRight(width, fill);
                }
            }
            return body;
        }

        private static string FormatBody(object value, char type, int prec, bool alt, bool comma, char sign)
        {
            string ApplySign(string s, bool neg)
            {
                if (neg) return s;
                if (sign == '+') return "+" + s;
                if (sign == ' ') return " " + s;
                return s;
            }

            switch (type)
            {
                case 'd':
                {
                    long v = PyOps.ToLong(value);
                    string s = Math.Abs(v).ToString(CultureInfo.InvariantCulture);
                    if (comma) s = GroupThousands(s);
                    s = (v < 0 ? "-" : "") + s;
                    return ApplySign(s, v < 0);
                }
                case 'f':
                case 'F':
                {
                    double d = PyOps.ToDouble(value);
                    int p = prec < 0 ? 6 : prec;
                    string s = Math.Abs(d).ToString("F" + p, CultureInfo.InvariantCulture);
                    if (comma) s = GroupThousands(s);
                    s = (d < 0 ? "-" : "") + s;
                    return ApplySign(s, d < 0);
                }
                case 'e':
                case 'E':
                {
                    double d = PyOps.ToDouble(value);
                    int p = prec < 0 ? 6 : prec;
                    string s = d.ToString((type == 'e' ? "e" : "E") + p, CultureInfo.InvariantCulture);
                    s = FixExponent(s, type);
                    return ApplySign(s, d < 0);
                }
                case 'g':
                case 'G':
                {
                    double d = PyOps.ToDouble(value);
                    int p = prec < 0 ? 6 : (prec == 0 ? 1 : prec);
                    string s = d.ToString((type == 'g' ? "G" : "G") + p, CultureInfo.InvariantCulture);
                    if (type == 'g') s = s.Replace("E", "e");
                    s = FixExponent(s, type == 'g' ? 'e' : 'E');
                    return ApplySign(s, d < 0);
                }
                case '%':
                {
                    double d = PyOps.ToDouble(value) * 100.0;
                    int p = prec < 0 ? 6 : prec;
                    string s = d.ToString("F" + p, CultureInfo.InvariantCulture) + "%";
                    return ApplySign(s, d < 0);
                }
                case 'x': return (alt ? "0x" : "") + PyOps.ToLong(value).ToString("x", CultureInfo.InvariantCulture);
                case 'X': return (alt ? "0X" : "") + PyOps.ToLong(value).ToString("X", CultureInfo.InvariantCulture);
                case 'o': return (alt ? "0o" : "") + Convert.ToString(PyOps.ToLong(value), 8);
                case 'b': return (alt ? "0b" : "") + Convert.ToString(PyOps.ToLong(value), 2);
                case 'c': return ((char)PyOps.ToLong(value)).ToString();
                case 's':
                case '\0':
                    if (type == '\0' && value is double dd)
                    {
                        if (prec >= 0) return ApplySign(PyOps.ToDouble(value).ToString("G" + Math.Max(1, prec), CultureInfo.InvariantCulture), dd < 0);
                        return PyOps.Str(value);
                    }
                    if (type == '\0' && (value is long || value is bool) && comma)
                        return GroupThousands(PyOps.Str(value));
                    return PyOps.Str(value);
                default:
                    return PyOps.Str(value);
            }
        }

        private static string FixExponent(string s, char e)
        {
            // .NET produce e+003; Python usa e+03 (mínimo 2 dígitos)
            int idx = s.IndexOfAny(new[] { 'e', 'E' });
            if (idx < 0) return s;
            string mant = s.Substring(0, idx);
            char ec = s[idx];
            string exp = s.Substring(idx + 1);
            char esign = '+';
            if (exp.Length > 0 && (exp[0] == '+' || exp[0] == '-')) { esign = exp[0]; exp = exp.Substring(1); }
            exp = exp.TrimStart('0');
            if (exp.Length < 2) exp = exp.PadLeft(2, '0');
            return mant + ec + esign + exp;
        }

        private static string GroupThousands(string num)
        {
            int dot = num.IndexOf('.');
            string intPart = dot < 0 ? num : num.Substring(0, dot);
            string frac = dot < 0 ? "" : num.Substring(dot);
            var sb = new StringBuilder();
            int cnt = 0;
            for (int i = intPart.Length - 1; i >= 0; i--)
            {
                sb.Insert(0, intPart[i]);
                if (++cnt % 3 == 0 && i > 0) sb.Insert(0, ',');
            }
            return sb.ToString() + frac;
        }

        // ── Operador % (printf-style) ──
        public static string PercentFormat(string fmt, object arg)
        {
            // arg puede ser una tupla (varios) o un valor único
            List<object> args = arg is PyTuple t ? t.Items : new List<object> { arg };
            var sb = new StringBuilder();
            int ai = 0;
            for (int i = 0; i < fmt.Length; i++)
            {
                char c = fmt[i];
                if (c != '%') { sb.Append(c); continue; }
                i++;
                if (i >= fmt.Length) break;
                if (fmt[i] == '%') { sb.Append('%'); continue; }
                // parse flags/width/.prec/type  (printf estilo C: [-+ 0#][width][.prec]type)
                string flags = "";
                while (i < fmt.Length && "+-# 0".IndexOf(fmt[i]) >= 0) { flags += fmt[i]; i++; }
                int width = 0; bool hasWidth = false;
                while (i < fmt.Length && char.IsDigit(fmt[i])) { width = width * 10 + (fmt[i] - '0'); i++; hasWidth = true; }
                int prec = -1;
                if (i < fmt.Length && fmt[i] == '.')
                {
                    i++; prec = 0;
                    while (i < fmt.Length && char.IsDigit(fmt[i])) { prec = prec * 10 + (fmt[i] - '0'); i++; }
                }
                char type = i < fmt.Length ? fmt[i] : 's';
                object val = ai < args.Count ? args[ai++] : null;
                bool leftAlign = flags.IndexOf('-') >= 0;
                bool zeroPad = flags.IndexOf('0') >= 0 && !leftAlign;
                string sgn = flags.IndexOf('+') >= 0 ? "+" : (flags.IndexOf(' ') >= 0 ? " " : "");
                string body;
                if (type == 'r') body = PyOps.Repr(val);
                else if (type == 's')
                {
                    body = PyOps.Str(val);
                    if (prec >= 0 && body.Length > prec) body = body.Substring(0, prec);
                }
                else
                {
                    string spec = sgn + type switch
                    {
                        'd' or 'i' => "d",
                        'f' or 'F' => (prec >= 0 ? "." + prec : "") + "f",
                        'e' or 'E' => (prec >= 0 ? "." + prec : "") + type,
                        'g' or 'G' => (prec >= 0 ? "." + prec : "") + type,
                        'x' => "x", 'X' => "X", 'o' => "o",
                        _ => "s"
                    };
                    body = FormatSpec(val, spec);
                }
                if (hasWidth && body.Length < width)   // aplicar ANCHO + justificación (lo que faltaba)
                {
                    int pad = width - body.Length;
                    if (leftAlign) body += new string(' ', pad);
                    else if (zeroPad && type != 's' && type != 'r')
                    {
                        int s0 = (body.Length > 0 && (body[0] == '-' || body[0] == '+' || body[0] == ' ')) ? 1 : 0;
                        body = body.Substring(0, s0) + new string('0', pad) + body.Substring(s0);
                    }
                    else body = new string(' ', pad) + body;
                }
                sb.Append(body);
            }
            return sb.ToString();
        }
    }
}
