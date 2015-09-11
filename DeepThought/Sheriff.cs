using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepThought
{
    public static class Sheriff
    {
        /// <summary>
        /// Let the damn "if"s go to hell
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="ex"></param>
        public static void Assert(bool expression, string message)
        {
            if (!expression)
            {
                throw new Exception(message);
            }
        }
    }
}
