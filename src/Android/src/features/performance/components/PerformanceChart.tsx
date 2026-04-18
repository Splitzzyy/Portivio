import React from 'react';
import { Dimensions, View } from 'react-native';
import {
  VictoryAxis,
  VictoryChart,
  VictoryLine,
  VictoryTheme,
} from 'victory-native';
import type { Performance } from '../../../types/dtos';

interface Props {
  history: Performance.Response[];
}

export default function PerformanceChart({ history }: Props): React.JSX.Element {
  const sorted = [...history].sort((a, b) => a.date.localeCompare(b.date));
  const data = sorted.map((p) => ({
    x: new Date(p.date),
    y: Number(p.currentValue),
  }));
  const width = Dimensions.get('window').width - 64;

  return (
    <View>
      <VictoryChart theme={VictoryTheme.material} width={width} scale={{ x: 'time' }}>
        <VictoryAxis fixLabelOverlap />
        <VictoryAxis dependentAxis />
        <VictoryLine data={data} interpolation="monotoneX" />
      </VictoryChart>
    </View>
  );
}
