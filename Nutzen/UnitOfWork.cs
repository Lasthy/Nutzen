using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nutzen;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class UnitOfWorkAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RequestAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class HandlerAttribute : Attribute
{
}