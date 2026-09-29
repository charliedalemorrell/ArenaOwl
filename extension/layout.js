// Splits an area into tiles for 1-4 screens:
//   1: full   2: side by side   3: one big on the left, two stacked on the right   4: 2x2 grid
// area is { left, top, width, height }. Returns an array of { left, top, width, height }.
export function layout(area, count) {
  const { left, top, width, height } = area;
  const halfW = Math.floor(width / 2);
  const halfH = Math.floor(height / 2);
  const rightX = left + halfW;
  const bottomY = top + halfH;

  switch (count) {
    case 1:
      return [{ left, top, width, height }];
    case 2:
      return [
        { left, top, width: halfW, height },
        { left: rightX, top, width: width - halfW, height },
      ];
    case 3:
      return [
        { left, top, width: halfW, height },
        { left: rightX, top, width: width - halfW, height: halfH },
        { left: rightX, top: bottomY, width: width - halfW, height: height - halfH },
      ];
    default:
      return [
        { left, top, width: halfW, height: halfH },
        { left: rightX, top, width: width - halfW, height: halfH },
        { left, top: bottomY, width: halfW, height: height - halfH },
        { left: rightX, top: bottomY, width: width - halfW, height: height - halfH },
      ];
  }
}
