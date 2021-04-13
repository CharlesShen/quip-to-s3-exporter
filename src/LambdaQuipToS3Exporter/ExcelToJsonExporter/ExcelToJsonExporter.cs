using Newtonsoft.Json.Linq;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExcelToJsonExporter
{
    public class ExcelToJsonExporter
    {
        private Stream _excelDataStream;
        private XSSFWorkbook _workbook;
        private IDictionary<int, SheetContext> _sheetContexts = new Dictionary<int, SheetContext>();

        private const string _identifierRegexGroupName = "identifier";
        private const string _arrayIndexRegexGroupName = "arrayIndex";
        private static Regex _propertyRegex = new Regex(@$"^(?<{_identifierRegexGroupName}>\w+)(\[(?<{_arrayIndexRegexGroupName}>\d+)\])?$", RegexOptions.Singleline | RegexOptions.Compiled);


        private JToken generateJsonObject(SheetContext sheet, string identifierPrefix, IDictionary<string, Property> childProperties, IRow row)
        {
            var jObject = new JObject();

            foreach (var childProperty in childProperties)
            {
                var childIdentifier = $"{identifierPrefix}.{childProperty.Key}";
                var childJToken = generateJsonToken(sheet, childIdentifier, childProperty.Value, row);
                jObject.Add(childProperty.Key, childJToken);
            }

            return jObject;
        }

        public JToken generateJsonPrimitive(SheetContext sheetContext, string identifier, IRow row)
        {
            int columnIndex;
            if (!sheetContext.ColumnNameIndex.TryGetValue(identifier, out columnIndex))
            {
                //if column name cannot be found, the only possible reason is that there is a gap in the array indices
                throw new Exception($"Cannot find column \"{identifier}\". Arrays indices must be contiguous with no gaps.");
            }

            JValue jValue = null;

            var cell = row.GetCell(columnIndex);
            var cellType = cell.CellType;

            if (cellType == CellType.Formula)
            {
                cellType = cell.CachedFormulaResultType;
            }

            switch (cellType)
            {
                case CellType.Blank:
                    {
                        jValue = JValue.CreateNull();
                        break;
                    }
                case CellType.Boolean:
                    {
                        jValue = new JValue(cell.BooleanCellValue);
                        break;
                    }
                case CellType.Numeric:
                    {
                        if (DateUtil.IsCellDateFormatted(cell))
                        {
                            jValue = new JValue(cell.DateCellValue);
                        }
                        else
                        {
                            var value = cell.NumericCellValue;

                            //if double value is a whole number, treat type as long
                            jValue = (value % 1 == 0) ? new JValue((long)value) : new JValue(value);
                        }

                        break;
                    }
                case CellType.String:
                    {
                        var value = cell.StringCellValue;
                        jValue = string.IsNullOrWhiteSpace(value) ? JValue.CreateNull() : new JValue(cell.StringCellValue);
                        break;
                    }
                case CellType.Error:
                    {
                        jValue = new JValue($"#ERROR: {FormulaError.ForInt(cell.ErrorCellValue).ToString()}");
                        break;
                    }
                    //case CellType.Unknown:
                    //    {
                    //        break;
                    //    }
            }

            return jValue;
        }

        private JToken generateJsonToken(SheetContext sheetContext, string identifier, Property property, IRow row)
        {
            JToken jToken = null;

            if (property.maxArrayIndex.HasValue)
            {
                jToken = new JArray();

                for (var i = 0; i < property.maxArrayIndex.Value; i++)
                {
                    var identifierArray = $"{identifier}[{i}]";
                    var resultjToken = property.ChildProperties.Any() ? generateJsonObject(sheetContext, identifierArray, property.ChildProperties, row) : generateJsonPrimitive(sheetContext, identifierArray, row);
                    (jToken as JArray).Add(resultjToken);
                }
            }
            else
            {
                jToken = property.ChildProperties.Any() ? generateJsonObject(sheetContext, identifier, property.ChildProperties, row) : generateJsonPrimitive(sheetContext, identifier, row);
            }

            return jToken;
        }

        private void buildPropertyTree(IDictionary<string, Property> propertyScope, string fullyQualifiedPropertyName)
        {
            var tokens = fullyQualifiedPropertyName.Split('.');

            var currentPropertyScope = propertyScope;
            foreach (var token in tokens)
            {
                var regexMatch = _propertyRegex.Match(token);

                if (!regexMatch.Success)
                {
                    throw new Exception($"Invalid token \"{token}\" in column header \"{fullyQualifiedPropertyName}\". Identifier syntax cannot be parsed.");
                }

                var identifier = regexMatch.Groups[_identifierRegexGroupName].Value;
                var arrayIndex = int.TryParse(regexMatch.Groups[_arrayIndexRegexGroupName].Value, out int i) ? (int?)i : null;

                Property property;
                if (!currentPropertyScope.TryGetValue(identifier, out property))
                {
                    property = new Property();
                    currentPropertyScope.Add(identifier, property);
                }

                if (arrayIndex.HasValue)
                {
                    //track the highest array index
                    property.maxArrayIndex = Math.Max(property.maxArrayIndex ?? 0, arrayIndex.Value);
                }

                currentPropertyScope = property.ChildProperties;
            }
        }

        private SheetContext buildSheetContext(ISheet sheet)
        {
            var sheetContext = new SheetContext();

            var headerRow = sheet.GetRow(0);
            var cellCount = headerRow.LastCellNum;
            for (var i = 0; i < cellCount; i++)
            {
                var cell = headerRow.GetCell(i);
                if (cell == null || string.IsNullOrWhiteSpace(cell.ToString())) continue;

                var columnHeader = cell.StringCellValue;
                buildPropertyTree(sheetContext.Properties, cell.StringCellValue);
                sheetContext.ColumnNameIndex.Add(cell.StringCellValue, i);
            }

            return sheetContext;
        }

        public ExcelToJsonExporter(Stream excelDataStream)
        {
            _excelDataStream = excelDataStream;
            _excelDataStream.Position = 0;
            _workbook = new XSSFWorkbook(_excelDataStream);

            XSSFFormulaEvaluator.EvaluateAllFormulaCells(_workbook);
        }

        public int GetNumberOfSheets()
        {
            return _workbook.NumberOfSheets;
        }
        public string GetSheetName(int index)
        {
            return _workbook.GetSheetName(index);
        }

        public JToken ExportSheetToJson(int sheetIndex, bool emitSheetNameAsKey = false)
        {
            //returns unnamed JSON array unless second parameter is set to true
            var rootJArray = new JArray();
            var sheet = _workbook.GetSheetAt(sheetIndex);
            var sheetName = sheet.SheetName;

            SheetContext sheetContext;
            if (!_sheetContexts.TryGetValue(sheetIndex, out sheetContext))
            {
                //sheet hasn't been processed yet, extract column information and build a property tree for the sheet
                sheetContext = buildSheetContext(sheet);
                _sheetContexts.Add(sheetIndex, sheetContext);
            }

            for (int i = (sheet.FirstRowNum + 1); i <= sheet.LastRowNum; i++)
            {
                var rootJObject = new JObject();
                IRow row = sheet.GetRow(i);

                //ignore rows that are all blank cells or "string" cells that happen to be all empty strings
                if (row == null || row.Cells.All(d => (d.CellType == CellType.Blank) || ((d.CellType == CellType.String) && string.IsNullOrWhiteSpace(d.StringCellValue)))) continue;

                foreach (var property in sheetContext.Properties)
                {
                    var jToken = generateJsonToken(sheetContext, property.Key, property.Value, row);
                    rootJObject.Add(property.Key, jToken);
                }

                rootJArray.Add(rootJObject);
            }

            if (emitSheetNameAsKey)
            {
                var jObject = new JObject();
                jObject.Add(sheetName, rootJArray);
                return jObject;
            }
            else
            {
                return rootJArray;
            }
        }

        public JToken ExportWorkbookToJson(bool emitSheetNameAsKey = true)
        {
            JToken rootjToken = emitSheetNameAsKey ? (new JObject() as JToken) : new JArray();

            for (var i = 0; i < _workbook.NumberOfSheets; i++)
            {
                var result = ExportSheetToJson(i, emitSheetNameAsKey);

                if (emitSheetNameAsKey)
                {
                    (rootjToken as JObject).Merge(result);
                }
                else
                {
                    (rootjToken as JArray).Add(result);
                }
            }

            return rootjToken;
        }
    }
}
