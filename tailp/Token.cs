// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;

namespace TailP
{
    public class Token
    {
        public Types Type { get; private set; }
        public string Text { get; set; }
        public int ColorIndex { get; private set; }

        public Token(Types type, string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));

            if (type == Types.Show || type == Types.Highlight)
            {
                throw new ArgumentException("ctor with colorIndex must be used!");
            }

            Type = type;
            ColorIndex = 0;
        }

        public Token(Types type, string text, int colorIndex)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Type = type;
            ColorIndex = colorIndex;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Token;

            return !ReferenceEquals(obj, null) &&
                   Type == other.Type &&
                   Text == other.Text &&
                   ColorIndex == other.ColorIndex;
        }

        public override int GetHashCode() =>
            Type.GetHashCode() ^ Text.GetHashCode() ^ ColorIndex.GetHashCode();

        public override string ToString() => Text;
    }
}
