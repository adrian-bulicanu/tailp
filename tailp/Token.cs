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
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            if (type == Types.Show || type == Types.Highlight)
            {
                throw new ArgumentException("ctor with colorIndex must be used!");
            }

            Type = type;
            Text = text;
            ColorIndex = 0;
        }

        public Token(Types type, string text, int colorIndex)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            Type = type;
            Text = text;
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

        public override int GetHashCode()
        {
            return Type.GetHashCode() ^ Text.GetHashCode() ^ ColorIndex.GetHashCode();
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
