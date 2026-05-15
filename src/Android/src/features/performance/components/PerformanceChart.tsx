import React from 'react';
import { Dimensions, StyleSheet, View } from 'react-native';
import Svg, { Circle, Line, Path, Text as SvgText } from 'react-native-svg';
import type { Performance } from '../../../types/dtos';

interface Props {
  history: Performance.Response[];
}

export default function PerformanceChart({ history }: Props): React.JSX.Element {
  const sorted = [...history].sort((a, b) => a.date.localeCompare(b.date));
  const values = sorted.map((point) => Number(point.currentValue));
  const width = Math.max(Dimensions.get('window').width - 96, 240);
  const height = 220;
  const padding = { top: 16, right: 12, bottom: 32, left: 12 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const minValue = Math.min(...values);
  const maxValue = Math.max(...values);
  const valueRange = maxValue - minValue || 1;

  const points = sorted.map((point, index) => {
    const x =
      padding.left +
      (sorted.length === 1 ? chartWidth / 2 : (index / (sorted.length - 1)) * chartWidth);
    const y =
      padding.top + (1 - (Number(point.currentValue) - minValue) / valueRange) * chartHeight;

    return { x, y, date: point.date };
  });

  const linePath = points.map((point, index) =>
    `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(2)} ${point.y.toFixed(2)}`,
  );
  const gridLines = [0, 0.5, 1].map((ratio) => padding.top + ratio * chartHeight);
  const firstDate = sorted[0]?.date.slice(5) ?? '';
  const lastDate = sorted[sorted.length - 1]?.date.slice(5) ?? '';

  return (
    <View style={styles.container}>
      <Svg width={width} height={height} accessibilityLabel="Performance trend chart">
        {gridLines.map((y) => (
          <Line
            key={y}
            x1={padding.left}
            y1={y}
            x2={width - padding.right}
            y2={y}
            stroke="#E5E7EB"
            strokeWidth={1}
          />
        ))}
        <Path d={linePath.join(' ')} fill="none" stroke="#4F46E5" strokeWidth={2} />
        {points.map((point) => (
          <Circle key={point.date} cx={point.x} cy={point.y} r={3} fill="#4F46E5" />
        ))}
        <SvgText x={padding.left} y={height - 8} fontSize="11" fill="#6B7280">
          {firstDate}
        </SvgText>
        <SvgText x={width - padding.right} y={height - 8} fontSize="11" fill="#6B7280" textAnchor="end">
          {lastDate}
        </SvgText>
        <SvgText x={width - padding.right} y={padding.top - 4} fontSize="11" fill="#6B7280" textAnchor="end">
          {maxValue.toFixed(0)}
        </SvgText>
        <SvgText x={width - padding.right} y={padding.top + chartHeight + 12} fontSize="11" fill="#6B7280" textAnchor="end">
          {minValue.toFixed(0)}
        </SvgText>
      </Svg>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    width: '100%',
  },
});
