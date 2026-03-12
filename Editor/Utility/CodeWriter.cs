using System.Text;

namespace Ulink.Editor
{
    /// <summary>
    /// Indent-aware code emitter for the Ulink generator. Always uses \n line endings
    /// (no \r\n) so generated output is platform-consistent.
    /// Directive() emits without indentation — used for #if/#endif preprocessor lines.
    /// </summary>
    internal sealed class CodeWriter
    {
        private readonly StringBuilder _builder = new();
        private int _indent;

        public void Line(string text = "")
        {
            if (text.Length == 0)
                _builder.Append('\n');
            else
                _builder.Append(' ', _indent * 4).Append(text).Append('\n');
        }

        public void Directive(string text) => _builder.Append(text).Append('\n');

        public void OpenBlock(string header = "")
        {
            if (header.Length > 0) Line(header);
            Line("{");
            _indent++;
        }

        public void CloseBlock()
        {
            _indent--;
            Line("}");
        }

        public override string ToString() => _builder.ToString();
    }
}
