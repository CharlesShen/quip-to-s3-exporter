using System.Collections.Generic;

namespace ExcelToJsonExporter
{
    public class SheetContext
    {
        public IDictionary<string, Property> Properties { get; } = new Dictionary<string, Property>();
        public IDictionary<string, int> ColumnNameIndex { get; } = new Dictionary<string, int>();
    }
}
