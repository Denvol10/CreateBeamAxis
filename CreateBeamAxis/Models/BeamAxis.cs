using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateBeamAxis.Models
{
    public class BeamAxis
    {
        public double Distance { get; set; }

        public override string ToString()
        {
            return Distance.ToString();
        }
    }
}
