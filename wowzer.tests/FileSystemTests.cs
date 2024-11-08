using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.CASC;

namespace wowzer.tests
{
    [TestClass]
    public class FileSystemTests
    {
        [TestMethod]
        public void OpenFileSystem()
        {
            var filesystem = new FileSystem(@"D:/01 - Games/World of Warcraft/", "08bb65d7bb507e5ea8c94683913ac978", "f40a44cc2fb3ac88f42f91b3d16889da");
        }
    }
}
