import { createNavigationContainerRef, StackActions } from '@react-navigation/native';

export const navigationRef = createNavigationContainerRef();

export function resetToLogin(): void {
  if (navigationRef.isReady()) {
    navigationRef.dispatch(StackActions.replace('Login'));
  }
}
