using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Infinario;

namespace Infinario.Commands
{

    internal interface Command
    {
        void Execute(State state);
    }

}
