export interface LayoutRouteData {
  hideLayout?: boolean;
  fullWidth?: boolean;
}

export interface LayoutRouteFlags {
  hideLayout: boolean;
  fullWidth: boolean;
}

export const DEFAULT_LAYOUT_ROUTE_FLAGS: LayoutRouteFlags = {
  hideLayout: false,
  fullWidth: false
};