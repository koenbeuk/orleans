using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.CodeGenerator.Tests.Invokables;
public class NestedGenericInterfaceTests : GeneratorTestBase<NestedGenericInterfaceTests>
{
    public NestedGenericInterfaceTests(GeneratorTestFixture<NestedGenericInterfaceTests> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        public class Root<TRoot>
        {
            public interface IA<T1, T2, T3> : Orleans.IGrain { }
        }
        ";
}
