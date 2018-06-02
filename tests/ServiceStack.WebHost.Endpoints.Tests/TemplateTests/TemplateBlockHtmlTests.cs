﻿using NUnit.Framework;
using ServiceStack.Templates;
using ServiceStack.Text;

namespace ServiceStack.WebHost.Endpoints.Tests.TemplateTests
{
    public class TemplateBlockHtmlTests
    {
        class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }

            public Person() { }
            public Person(string name, int age)
            {
                Name = name;
                Age = age;
            }
        }

        [Test]
        public void Does_evaluate_void_img_html_block()
        {
            var context = new TemplateContext().Init();
            
            Assert.That(context.EvaluateTemplate("{{#img {alt:'image',src:'image.png'} }}{{/img}}"), 
                Is.EqualTo("<img alt=\"image\" src=\"image.png\">"));
        }

        [Test]
        public void Does_evaluate_ul_html_block()
        {
            var context = new TemplateContext {
                Args = {
                    ["numbers"] = new int[]{1, 2, 3},
                    ["letters"] = new[]{ "A", "B", "C" },
                }
            }.Init();
            
            Assert.That(context.EvaluateTemplate("{{#ul {class:'nav'} }} <li>item</li> {{/ul}}").RemoveNewLines(), 
                Is.EqualTo(@"<ul class=""nav""> <li>item</li> </ul>"));
            
            Assert.That(context.EvaluateTemplate("{{#ul {each:letters, class:'nav', id:'menu'} }}<li>{{it}}</li>{{/ul}}").RemoveNewLines(), 
                Is.EqualTo(@"<ul class=""nav"" id=""menu""><li>A</li><li>B</li><li>C</li></ul>"));
            
            Assert.That(context.EvaluateTemplate("{{#ul {each:numbers, it:'num'} }}<li>{{num}}</li>{{/ul}}").RemoveNewLines(), 
                Is.EqualTo(@"<ul><li>1</li><li>2</li><li>3</li></ul>"));
            
            Assert.That(context.EvaluateTemplate("{{#ul {each:none} }}<li>{{it}}</li>{{/ul}}").RemoveNewLines(), Is.EqualTo(@""));
            
            Assert.That(context.EvaluateTemplate("{{#ul {each:none} }}<li>{{it}}</li>{{else}}no items{{/ul}}").RemoveNewLines(), 
                Is.EqualTo(@"no items"));
        }

        [Test]
        public void Does_evaluate_ul_with_nested_html_blocks()
        {
            var context = new TemplateContext {
                Args = {
                    ["items"] = new[]{ new Person("foo", 1),new Person("bar", 2),new Person("baz", 3) },
                    ["id"] = "menu",
                    ["disclaimerAccepted"] = false,
                    ["highlight"] = "baz",
                }
            }.Init();

            var template = @"
{{#ul {each:items, class:['nav', !disclaimerAccepted?'blur':''], id:'ul-'+id} }}
    {{#li {class: {alt:isOdd(index), active:Name==highlight} }}
        {{Name}}
    {{/li}}
{{else}}
    <div>no items</div>
{{/ul}}";

            var result = context.EvaluateTemplate(template);
            
            Assert.That(result.NormalizeNewLines(), Is.EqualTo(@"
<ul class=""nav blur"" id=""ul-menu"">
    <li>
        foo
    </li>
    <li class=""alt"">
        bar
    </li>
    <li class=""active"">
        baz
    </li>
</ul>".NormalizeNewLines()));

            var withoutBlock = @"
{{#if !isEmpty(items)}}
<ul{{ ['nav', !disclaimerAccepted?'blur':''] | htmlClass }} id=""ul-{{id}}"">
{{#each items}}
    <li{{ {alt:isOdd(index), active:Name==highlight} | htmlClass }}>
        {{Name}}
    </li>
{{/each}}
</ul>
{{else}}
   <div>no items</div>
{{/if}}";

            var withoutBlockResult = context.EvaluateTemplate(withoutBlock);
            
            Assert.That(withoutBlockResult.RemoveNewLines(), Is.EqualTo(result.RemoveNewLines()));
        }
    }

}