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
        #pragma warning disable 1998
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<Test.MyComponent<string, int>>(0);
            __builder.AddAttribute(1, "Item", global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<string>(
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                                              "hi"

#line default
#line hidden
#nullable disable
            ));
            __builder.AddAttribute(2, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment<string>)((context) => (__builder2) => {
                __builder2.OpenElement(3, "div");
                __builder2.AddContent(4, 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
                      context.ToLower()

#line default
#line hidden
#nullable disable
                );
                __builder2.CloseElement();
            }
            ));
            __builder.AddAttribute(5, "AnotherChildContent", (Microsoft.AspNetCore.Components.RenderFragment<Test.MyComponent<string, int>.Context>)((item) => (__builder2) => {
                __builder2.AddContent(6, 
#nullable restore
#line 4 "x:\dir\subdir\Test\TestComponent.cshtml"
   System.Math.Max(0, item.Item)

#line default
#line hidden
#nullable disable
                );
                __builder2.AddMarkupContent(7, ";\r\n");
            }
            ));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
