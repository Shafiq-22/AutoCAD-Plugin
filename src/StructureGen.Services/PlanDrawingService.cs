using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Geometry;
using StructureGen.Geometry.Drawing;

namespace StructureGen.Services
{
    /// <summary>
    /// Composes two-dimensional plan drawings from the elements in a drawing, without touching
    /// AutoCAD: the AutoCAD layer only reads the elements in and writes the lines out.
    /// </summary>
    public sealed class PlanDrawingService
    {
        private readonly ElementGeneratorRegistry _generators;
        private readonly ILogger _logger;

        /// <summary>Initialises a new instance of the <see cref="PlanDrawingService"/> class.</summary>
        /// <param name="generators">The element generators, which supply each element's geometry.</param>
        /// <param name="logger">The logger.</param>
        public PlanDrawingService(ElementGeneratorRegistry generators, ILogger logger)
        {
            _generators = generators ?? throw new ArgumentNullException(nameof(generators));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Composes a plan drawing.</summary>
        /// <param name="elements">The elements to draw, with where each actually sits.</param>
        /// <param name="options">What the drawing should contain.</param>
        /// <returns>
        /// The drawing, in plan millimetres. An arrangement keeps the elements' true plan positions;
        /// an item sheet starts at the origin and runs downwards.
        /// </returns>
        public Result<PlanDrawing> Compose(IReadOnlyList<PlacedElement> elements, PlanDrawingOptions options)
        {
            if (elements is null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            options ??= new PlanDrawingOptions();

            if (elements.Count == 0)
            {
                return Result<PlanDrawing>.Failure(
                    "Plan.NoElements",
                    "No StructureGen elements were found, so there is nothing to draw a plan of. " +
                    "Use STRUCTUREGEN to create some first.");
            }

            ValidationReport report = new();
            Dictionary<string, ElementGeometry?> geometryCache = new(StringComparer.Ordinal);

            PlanDrawing drawing = options.Layout == PlanLayout.Items
                ? ComposeItems(elements, options, report, geometryCache)
                : ComposeArrangement(elements, options, report, geometryCache);

            if (drawing.Curves.Count == 0)
            {
                return Result<PlanDrawing>.Failure(
                    "Plan.NothingDrawn",
                    "None of the elements could be drawn in plan. " +
                    string.Join(" ", report.Messages.Take(3).Select(m => m.Message)),
                    report);
            }

            _logger.Information(
                $"Composed a {options.Layout} plan: {drawing.ElementCount} element(s), {drawing.ItemCount} item(s), " +
                $"{drawing.Curves.Count} line(s).");

            return Result<PlanDrawing>.Success(drawing, report);
        }

        /// <summary>Chooses a label height in proportion to the members being drawn.</summary>
        /// <param name="views">The plans being labelled.</param>
        /// <returns>The height in millimetres.</returns>
        public static double AutomaticTextHeight(IEnumerable<PlanView> views)
        {
            List<double> widths = views.Select(v => v.HalfWidthMm * 2d).Where(w => w > 0d).OrderBy(w => w).ToList();

            if (widths.Count == 0)
            {
                return 50d;
            }

            // Half the typical member width reads well against the member without swamping it.
            double median = widths[widths.Count / 2];
            return Math.Max(2.5d, Math.Min(500d, Math.Round(median * 0.5d, 1)));
        }

        /// <summary>Builds the text that identifies an item on a plan.</summary>
        /// <param name="definition">The item.</param>
        /// <returns>The label, such as "IPE 300 L=6000".</returns>
        public static string LabelFor(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            CategoryDescriptor category = CategoryCatalog.FindOrGeneric(definition.CategoryId);
            string label = definition.Designation;

            if (category.Kind != CategoryKind.Fastener && definition.LengthMm is { } length)
            {
                label += " L=" + length.ToString("0.###", CultureInfo.InvariantCulture);
            }

            return definition.IsCustom ? label + " (NON-STD)" : label;
        }

        private PlanDrawing ComposeArrangement(
            IReadOnlyList<PlacedElement> elements,
            PlanDrawingOptions options,
            ValidationReport report,
            Dictionary<string, ElementGeometry?> cache)
        {
            List<(PlacedElement Element, PlanView View)> views = new();

            foreach (PlacedElement element in elements)
            {
                ElementGeometry? geometry = GeometryFor(element.Definition, report, cache);
                if (geometry is null)
                {
                    continue;
                }

                views.Add((element, PlanViewBuilder.Build(geometry, element.Frame, options.CentreLines)));
            }

            List<PlanCurve> curves = views.SelectMany(v => v.View.Curves).ToList();
            List<PlanText> texts = new();

            if (views.Count == 0)
            {
                return new PlanDrawing(PlanLayout.Arrangement, curves, texts, 0, 0, report);
            }

            double height = options.TextHeightMm > 0d
                ? options.TextHeightMm
                : AutomaticTextHeight(views.Select(v => v.View));

            if (options.Labels)
            {
                foreach ((PlacedElement element, PlanView view) in views)
                {
                    // Fasteners are left unlabelled on an arrangement: there are usually hundreds of
                    // them, each smaller than its own label, and the schedule lists them anyway.
                    if (CategoryCatalog.FindOrGeneric(element.Definition.CategoryId).Kind == CategoryKind.Fastener)
                    {
                        continue;
                    }

                    texts.Add(LabelBeside(view, LabelFor(element.Definition), height));
                }
            }

            (Vec2 minimum, Vec2 maximum) = PlanView.BoundsOf(curves);
            texts.Add(new PlanText(
                "PLAN",
                new Vec2((minimum.X + maximum.X) / 2d, minimum.Y - (height * 4d)),
                height * 2d,
                0d,
                PlanTextAlignment.BottomCentre));

            int items = views.Select(v => ItemKey(v.Element.Definition)).Distinct(StringComparer.Ordinal).Count();
            return new PlanDrawing(PlanLayout.Arrangement, curves, texts, views.Count, items, report);
        }

        private PlanDrawing ComposeItems(
            IReadOnlyList<PlacedElement> elements,
            PlanDrawingOptions options,
            ValidationReport report,
            Dictionary<string, ElementGeometry?> cache)
        {
            var groups = elements
                .GroupBy(e => ItemKey(e.Definition), StringComparer.Ordinal)
                .Select(g => (Definition: g.First().Definition, Count: g.Count()))
                .OrderBy(g => CategoryOrder(g.Definition.CategoryId))
                .ThenBy(g => g.Definition.Designation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Definition.LengthMm ?? 0m)
                .ToList();

            List<(ElementDefinition Definition, int Count, PlanView View)> views = new();

            foreach ((ElementDefinition definition, int count) in groups)
            {
                ElementGeometry? geometry = GeometryFor(definition, report, cache);
                if (geometry is null)
                {
                    continue;
                }

                views.Add((definition, count, PlanViewBuilder.Build(geometry, ItemFrame(definition), options.CentreLines)));
            }

            List<PlanCurve> curves = new();
            List<PlanText> texts = new();

            if (views.Count == 0)
            {
                return new PlanDrawing(PlanLayout.Items, curves, texts, 0, 0, report);
            }

            double height = options.TextHeightMm > 0d
                ? options.TextHeightMm
                : AutomaticTextHeight(views.Select(v => v.View));

            double gap = height * 1.5d;
            double cursor = 0d;

            texts.Add(new PlanText("PLAN - ITEMS", new Vec2(0d, cursor + gap), height * 2d, 0d, PlanTextAlignment.BottomLeft));

            int item = 0;
            foreach ((ElementDefinition definition, int count, PlanView view) in views)
            {
                item++;

                if (options.Labels)
                {
                    cursor -= height;
                    texts.Add(new PlanText(
                        $"{item}. {LabelFor(definition)} - {count} No.",
                        new Vec2(0d, cursor),
                        height,
                        0d,
                        PlanTextAlignment.BottomLeft));
                    cursor -= height * 0.75d;
                }

                (Vec2 minimum, Vec2 maximum) = view.Bounds();
                Vec2 offset = new(-minimum.X, cursor - maximum.Y);
                curves.AddRange(view.Translated(offset).Curves);

                cursor -= (maximum.Y - minimum.Y) + (gap * 2d);
            }

            return new PlanDrawing(PlanLayout.Items, curves, texts, views.Sum(v => v.Count), views.Count, report);
        }

        private static PlanText LabelBeside(PlanView view, string text, double height)
        {
            if (view.IsEndView)
            {
                double clear = (view.HalfWidthMm * 0.75d) + (height * 0.4d);
                return new PlanText(
                    text, view.LabelAnchor + new Vec2(clear, clear), height, 0d, PlanTextAlignment.BottomLeft);
            }

            // To the left of the reading direction, which after the angle has been made readable is
            // always the upper side of the member.
            Vec2 across = new(-Math.Sin(view.LabelAngle), Math.Cos(view.LabelAngle));
            Vec2 position = view.LabelAnchor + (across * (view.HalfWidthMm + (height * 0.4d)));
            return new PlanText(text, position, height, view.LabelAngle, PlanTextAlignment.BottomCentre);
        }

        /// <summary>
        /// The frame an item is drawn in on an item sheet: linear members and plates lie level along
        /// plan X, which shows their top face; fasteners stand upright, head up, as they are fitted
        /// through a level flange.
        /// </summary>
        private static ElementFrame ItemFrame(ElementDefinition definition)
        {
            Placement placement = CategoryCatalog.FindOrGeneric(definition.CategoryId).Kind == CategoryKind.Fastener
                ? new Placement(Vec3.Origin, new Vec3(0d, 0d, -1d))
                : new Placement(Vec3.Origin, Vec3.UnitX);

            return ElementFrame.FromPlacement(placement);
        }

        private static string ItemKey(ElementDefinition definition) =>
            BlockNaming.BlockNameFor(definition, RepresentationMode.Solid3D);

        private static int CategoryOrder(string categoryId)
        {
            IReadOnlyList<CategoryDescriptor> all = CategoryCatalog.All;

            for (int index = 0; index < all.Count; index++)
            {
                if (string.Equals(all[index].Id, categoryId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return all.Count;
        }

        private ElementGeometry? GeometryFor(
            ElementDefinition definition, ValidationReport report, Dictionary<string, ElementGeometry?> cache)
        {
            string key = ItemKey(definition);

            if (cache.TryGetValue(key, out ElementGeometry? cached))
            {
                return cached;
            }

            ElementGeometry? geometry = null;
            IElementGenerator? generator = _generators.Find(definition.CategoryId);

            if (generator is null)
            {
                report.AddWarning(
                    "Plan.NoGenerator",
                    $"'{definition.Designation}' was left off the plan: no geometry generator is registered for the " +
                    $"category '{definition.CategoryId}'.",
                    definition.Designation);
            }
            else
            {
                Result<ElementGeometry> built = generator.BuildGeometry(definition);

                if (built.IsSuccess)
                {
                    geometry = built.Value;
                }
                else
                {
                    report.AddWarning(
                        "Plan.GeometryFailed",
                        $"'{definition.Designation}' was left off the plan: {built.ErrorMessage}",
                        definition.Designation);
                }
            }

            cache[key] = geometry;
            return geometry;
        }
    }
}
