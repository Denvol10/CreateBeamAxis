using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CreateBeamAxis.Models.Filters;

namespace CreateBeamAxis.Models
{
    public class RevitGeometryUtils
    {
        // Выбор оси трассы
        public static List<Curve> GetCurvesByRectangle(UIApplication uiapp, out string elementIds)
        {
            Selection sel = uiapp.ActiveUIDocument.Selection;
            var selectedElements = sel.PickElementsByRectangle("Select Road Axis");
            var directshapeRoadAxis = selectedElements.OfType<DirectShape>();
            elementIds = ElementIdToString(directshapeRoadAxis);
            var curvesRoadAxis = GetCurvesByDirectShapes(directshapeRoadAxis);

            return curvesRoadAxis;
        }

        // Получение линий границ блоков ПС
        public static List<Line> GetCurvesByLines(UIApplication uiapp, out string elementIds, out SketchPlane sketchPlane)
        {
            Selection sel = uiapp.ActiveUIDocument.Selection;
            var elements = sel.PickElementsByRectangle(new ModelLineElementFilter(), "Select Lines");
            var firstModelLine = elements.FirstOrDefault() as ModelLine;
            sketchPlane = firstModelLine.SketchPlane;
            Options options = new Options();
            elementIds = ElementIdToString(elements);
            var lines = elements.Select(e => e.get_Geometry(options).First()).OfType<Line>().ToList();

            return lines;
        }

        // Получение линий границ блоков ПС по их id
        public static List<Line> GetProfileLinesById(Document doc, IEnumerable<int> ids, out SketchPlane sketchPlane)
        {
            var elementsInSettings = new List<Element>();
            foreach (var id in ids)
            {
                ElementId elemId = new ElementId(id);
                Element elem = doc.GetElement(elemId);
                elementsInSettings.Add(elem);
            }

            var firstElement = elementsInSettings.FirstOrDefault() as ModelLine;
            sketchPlane = firstElement.SketchPlane;

            Options options = new Options();
            var lines = elementsInSettings.Select(e => e.get_Geometry(options).First()).OfType<Line>().ToList();

            return lines;
        }

        // Получение начальной линии
        public static Curve GetStartLine(UIApplication uiapp, out string elementIds, out SketchPlane sketchPlane)
        {
            Selection sel = uiapp.ActiveUIDocument.Selection;
            var boundCurvePicked = sel.PickObject(ObjectType.Element, new ModelLineElementFilter() ,"Выберете начальную линию");
            Options options = new Options();
            Element curveElement = uiapp.ActiveUIDocument.Document.GetElement(boundCurvePicked);
            var modelCurve = curveElement as ModelCurve;
            sketchPlane = modelCurve.SketchPlane;
            elementIds = "Id" + curveElement.Id.IntegerValue;
            var boundCurve = curveElement.get_Geometry(options).First() as Curve;

            return boundCurve;
        }

        // Получение линии по Id
        public static Curve GetStartLineById(Document doc, string elemIdInSettings, out SketchPlane sketchPlane)
        {
            var elemId = GetIdsByString(elemIdInSettings).First();
            ElementId modelLineId = new ElementId(elemId);
            var modelLine = doc.GetElement(modelLineId) as ModelCurve;
            sketchPlane = modelLine.SketchPlane;
            Options options = new Options();
            Curve line = modelLine.get_Geometry(options).First() as Curve;

            return line;
        }

        // Проверка на то существуют ли элементы с данным Id в модели
        public static bool IsElemsExistInModel(Document doc, IEnumerable<int> elems, Type type)
        {
            if (elems is null)
            {
                return false;
            }

            foreach (var elem in elems)
            {
                ElementId id = new ElementId(elem);
                Element curElem = doc.GetElement(id);
                if (curElem is null || !(curElem.GetType() == type))
                {
                    return false;
                }
            }

            return true;
        }

        // Получение id элементов на основе списка в виде строки
        public static List<int> GetIdsByString(string elems)
        {
            if (string.IsNullOrEmpty(elems))
            {
                return null;
            }

            var elemIds = elems.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => int.Parse(s.Remove(0, 2)))
                         .ToList();

            return elemIds;
        }

        // Получение линий по их id
        public static List<Curve> GetCurvesById(Document doc, IEnumerable<int> ids)
        {
            var directShapeLines = new List<DirectShape>();
            foreach (var id in ids)
            {
                ElementId elemId = new ElementId(id);
                DirectShape line = doc.GetElement(elemId) as DirectShape;
                directShapeLines.Add(line);
            }

            var lines = GetCurvesByDirectShapes(directShapeLines).OfType<Curve>().ToList();

            return lines;
        }

        // Метод получения строки с ElementId
        private static string ElementIdToString(IEnumerable<Element> elements)
        {
            var stringArr = elements.Select(e => "Id" + e.Id.IntegerValue.ToString()).ToArray();
            string resultString = string.Join(", ", stringArr);

            return resultString;
        }

        // Получение линий на основе элементов DirectShape
        private static List<Curve> GetCurvesByDirectShapes(IEnumerable<DirectShape> directShapes)
        {
            var curves = new List<Curve>();

            Options options = new Options();
            var geometries = directShapes.Select(d => d.get_Geometry(options)).SelectMany(g => g);

            foreach (var geom in geometries)
            {
                if (geom is PolyLine polyLine)
                {
                    var polyCurve = GetCurvesByPolyline(polyLine);
                    curves.AddRange(polyCurve);
                }
                else
                {
                    curves.Add(geom as Curve);
                }
            }

            return curves;
        }

        // Метод получения списка линий на основе полилинии
        private static IEnumerable<Curve> GetCurvesByPolyline(PolyLine polyLine)
        {
            var curves = new List<Curve>();

            for (int i = 0; i < polyLine.NumberOfCoordinates - 1; i++)
            {
                var line = Line.CreateBound(polyLine.GetCoordinate(i), polyLine.GetCoordinate(i + 1));
                curves.Add(line);
            }

            return curves;
        }
    }
}
