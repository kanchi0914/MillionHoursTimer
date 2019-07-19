using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHTimer
{
    public class Sample
    {
        private String name = "";

        public string Name { get => name; set => name = value; }

        public override bool Equals(object obj)
        {
            Sample sample = (Sample)obj;
            return (Name == sample.Name);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

    }
}
