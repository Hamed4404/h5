// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public partial class TestComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 219
        private void __RazorDirectiveTokenHelpers__() {
        }
        #pragma warning restore 219
        #pragma warning disable 0414
        private static System.Object __o = null;
        #pragma warning restore 0414
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __o = typeof(
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
             DateTime

#line default
#line hidden
#nullable disable
            );
            __o = global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<System.Collections.Generic.IEnumerable<DateTime>>(
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                Array.Empty<DateTime>()

#line default
#line hidden
#nullable disable
            );
            __builder.AddAttribute(-1, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)((__builder2) => {
                __Blazor.Test.TestComponent.TypeInference.CreateColumn_0(__builder2, -1, default(DateTime));
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
__o = typeof(Column<>);

#line default
#line hidden
#nullable disable
                __Blazor.Test.TestComponent.TypeInference.CreateColumn_1(__builder2, -1, default(DateTime));
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
__o = typeof(Column<>);

#line default
#line hidden
#nullable disable
            }
            ));
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
__o = typeof(Grid<>);

#line default
#line hidden
#nullable disable
        }
        #pragma warning restore 1998
    }
}
namespace __Blazor.Test.TestComponent
{
    #line hidden
    internal static class TypeInference
    {
        public static void CreateColumn_0<TItem>(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder, int seq, TItem __syntheticArg0)
        {
        __builder.OpenComponent<global::Test.Column<TItem>>(seq);
        __builder.CloseComponent();
        }
        public static void CreateColumn_1<TItem>(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder, int seq, TItem __syntheticArg0)
        {
        __builder.OpenComponent<global::Test.Column<TItem>>(seq);
        __builder.CloseComponent();
        }
    }
}
#pragma warning restore 1591
