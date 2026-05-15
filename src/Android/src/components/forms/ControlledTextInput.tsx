import React from 'react';
import { Controller, FieldValues, Path, UseControllerProps } from 'react-hook-form';
import { TextInput, HelperText } from 'react-native-paper';
import { View } from 'react-native';

interface Props<T extends FieldValues>
  extends UseControllerProps<T>,
    Omit<
      React.ComponentProps<typeof TextInput>,
      'value' | 'onChangeText' | 'onBlur' | 'error' | 'defaultValue'
    > {
  name: Path<T>;
  label: string;
}

export function ControlledTextInput<T extends FieldValues>({
  name,
  control,
  rules,
  defaultValue,
  label,
  ...rest
}: Props<T>): React.JSX.Element {
  return (
    <Controller<T>
      name={name}
      control={control}
      rules={rules}
      defaultValue={defaultValue}
      render={({ field: { value, onChange, onBlur }, fieldState: { error } }) => (
        <View style={{ marginBottom: 8 }}>
          <TextInput
            mode="outlined"
            label={label}
            value={value as string | undefined ?? ''}
            onChangeText={onChange}
            onBlur={onBlur}
            error={!!error}
            {...rest}
          />
          <HelperText type="error" visible={!!error}>
            {error?.message ?? ' '}
          </HelperText>
        </View>
      )}
    />
  );
}
