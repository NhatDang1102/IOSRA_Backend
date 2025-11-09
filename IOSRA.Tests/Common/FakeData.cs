using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IOSRA.Tests.Common;

public static class FakeData
{
    public static Guid G() => Guid.NewGuid();

    public static List<int> Distribution(params int[] stars) => stars.ToList(); // placeholder
}

