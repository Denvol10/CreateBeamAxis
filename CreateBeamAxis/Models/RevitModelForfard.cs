using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;
using System.Collections.ObjectModel;
using BridgeDeck.Models;
using CreateBeamAxis.Models;
using CreateBeamAxis.Infrastructure;

namespace CreateBeamAxis
{
    public class RevitModelForfard
    {
        private UIApplication Uiapp { get; set; } = null;
        private Application App { get; set; } = null;
        private UIDocument Uidoc { get; set; } = null;
        private Document Doc { get; set; } = null;

        public RevitModelForfard(UIApplication uiapp)
        {
            Uiapp = uiapp;
            App = uiapp.Application;
            Uidoc = uiapp.ActiveUIDocument;
            Doc = uiapp.ActiveUIDocument.Document;
        }

        #region Проверка на то существуют линии оси и линии на поверхности в модели
        public bool IsLinesExistInModel(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);

            return RevitGeometryUtils.IsElemsExistInModel(Doc, elemIds, typeof(DirectShape));
        }
        #endregion

        #region Ось трассы
        public PolyCurve RoadAxis { get; set; }

        private string _roadAxisElemIds;

        public string RoadAxisElemIds
        {
            get => _roadAxisElemIds;
            set => _roadAxisElemIds = value;
        }

        public void GetPolyCurve()
        {
            var curves = RevitGeometryUtils.GetCurvesByRectangle(Uiapp, out _roadAxisElemIds);
            RoadAxis = new PolyCurve(curves);
        }
        #endregion

        #region Получение оси трассы из Settings
        public void GetAxisBySettings(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);
            var lines = RevitGeometryUtils.GetCurvesById(Doc, elemIds);
            RoadAxis = new PolyCurve(lines);
        }
        #endregion

        #region Плоскость для построения линий
        private SketchPlane _sketchPlane;
        #endregion

        #region Начальная линия
        public Curve StartLine { get; set; }

        private string _startLineElemIds;
        public string StartLineElemIds
        {
            get => _startLineElemIds;
            set => _startLineElemIds = value;
        }

        public void GetStartLine()
        {
            StartLine = RevitGeometryUtils.GetStartLine(Uiapp, out _startLineElemIds, out _sketchPlane);
        }

        public void GetStartLineBySettings(string elementId)
        {
            StartLine = RevitGeometryUtils.GetStartLineById(Doc, elementId, out _sketchPlane);
        }
        #endregion

        #region Проверка на то существует начальная линия
        public bool IsStartLineExistInModel(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);

            return RevitGeometryUtils.IsElemsExistInModel(Doc, elemIds, typeof(ModelLine));
        }
        #endregion

        #region Создание линий
        public void CreateLines(double distBetweenLines, bool isChangeDirection, int countLines, double lineLength)
        {
            double startParameter;
            bool isStartLineIntersectAxis = RoadAxis.Intersect(StartLine, out _, out startParameter);
            if(!isStartLineIntersectAxis)
            {
                TaskDialog.Show("Предупреждение", "Линия не пересекается с осью");
                return;
            }

            var newLineParameters = new List<double>();

            double dist = UnitUtils.ConvertToInternalUnits(distBetweenLines, UnitTypeId.Meters);
            for(int i = 1; i <= countLines; i++)
            {
                if(isChangeDirection)
                {
                    newLineParameters.Add(startParameter + dist * i);
                }
                else
                {
                    newLineParameters.Add(startParameter - dist * i);
                }
            }

            if(!(newLineParameters.Min() >= 0 && newLineParameters.Max() <= RoadAxis.GetLength()))
            {
                TaskDialog.Show("Предупреждение", "Линии выходят за границы оси");
                return;
            }

            var planes = new List<Plane>();

            foreach(var param in newLineParameters)
            {
                planes.Add(RoadAxis.GetPlaneOnPolycurve(param));
            }

            using(Transaction trans = new Transaction(Doc, "Created Model Lines"))
            {
                trans.Start();
                foreach(var plane in planes)
                {
                    double halfLine = UnitUtils.ConvertToInternalUnits(lineLength / 2, UnitTypeId.Meters);
                    XYZ vector = plane.XVec;
                    if(vector.Z != 0)
                    {
                        vector = plane.YVec;
                    }
                    XYZ point1 = plane.Origin + vector.Normalize() * halfLine;
                    XYZ point2 = plane.Origin - vector.Normalize() * halfLine;

                    Line line = Line.CreateBound(point1, point2);
                    if(Doc.IsFamilyDocument)
                    {
                        ModelCurve modelCurve = Doc.FamilyCreate.NewModelCurve(line, _sketchPlane);
                    }
                    else
                    {
                        ModelCurve modelCurve = Doc.Create.NewModelCurve(line, _sketchPlane);

                    }
                }
                trans.Commit();
            }
        }
        #endregion

    }
}
