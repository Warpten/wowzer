using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wowzer.fs.CASC
{
    public class Installation
    {
        private readonly Configuration _cdnConfiguration;
        private readonly Configuration _buildConfiguration;
        private readonly Index[] _indices = [];
        private readonly Encoding _encoding;
        private readonly Root? _root;
    }
}
