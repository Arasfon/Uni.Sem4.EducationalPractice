using System.Collections.Generic;

namespace Uni.EducationalPractice4;

public readonly record struct OdeRootSearchResult(
    Point Root,
    List<Point>? AllOdeSolutionPoints
);
