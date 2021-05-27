using System.Collections.Generic;

namespace ExcelToJsonExporter
{
    public class Property
    {
        public IDictionary<string, Property> ChildProperties = new Dictionary<string, Property>();
        public int? maxArrayIndex = null;
    }
}
