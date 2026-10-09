// Types pour les entités de l'API

export interface Product {
  id: number;
  code: string;
  name: string;
  description: string;
  unitPrice: number;
  costPrice: number;
  unit: string;
  defaultLocationId?: number;
  stockQuantity?: number;
}

export interface CreateProductDto {
  code: string;
  name: string;
  description?: string;
  unitPrice: number;
  costPrice: number;
  unit: string;
  requiresLotTracking: boolean;
  requiresSerialTracking: boolean;
  defaultLocationId?: number;
}

export interface Order {
  id: number;
  orderNumber: string;
  customerName: string;
  customerEmail?: string;
  customerAddress?: string;
  status: string;
  orderDate: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  items: OrderItem[];
}

export interface OrderItem {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  warehouseId: number;
  warehouseName: string;
  quantity: number;
  shippedQuantity: number;
  reservedQuantity: number;
  unitPrice: number;
  discount: number;
  lineTotal: number;
}

export interface CreateOrderDto {
  customerName: string;
  customerEmail?: string;
  customerAddress?: string;
  taxRate?: number;
  notes?: string;
  items: CreateOrderItemDto[];
}

export interface CreateOrderItemDto {
  productId: number;
  warehouseId: number;
  quantity: number;
  unitPrice?: number;
  discount?: number;
}

export interface Invoice {
  id: number;
  orderId: number;
  invoiceNumber: string;
  orderNumber: string;
  invoiceDate: string;
  dueDate?: string;
  status: string;
  customerName: string;
  customerEmail?: string;
  customerAddress?: string;
  subTotal: number;
  taxRate: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  items: InvoiceItem[];
}

export interface InvoiceItem {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discount: number;
  taxRate: number;
  lineTotal: number;
}

export interface Stock {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  warehouseId: number;
  warehouseName: string;
  locationId?: number;
  locationName?: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  averageCost: number;
  reorderPoint: number;
  lastUpdated: string;
}

export interface Warehouse {
  id: number;
  code: string;
  name: string;
  address: string;
  city: string;
  country: string;
}

export interface DemandForecast {
  product_id: number;
  product_code: string;
  product_name: string;
  forecasts: Array<{
    mois: string;
    prediction: number;
  }>;
  message?: string;
}

export interface Optimization {
  product_id: number;
  product_code: string;
  product_name: string;
  current_stock: number;
  reorder_point: number;
  eoq: number;
  average_daily_demand: number;
  lead_time_days: number;
  safety_stock: number;
  unit_cost: number;
}

export interface Supplier {
  id: number;
  name: string;
  contactName?: string;
  email?: string;
  phone?: string;
  address?: string;
}

export interface PurchaseOrder {
  id: number;
  orderNumber: string;
  supplierId: number;
  supplier?: Supplier;
  orderDate: string;
  expectedDate?: string;
  status: string;
  notes?: string;
  totalAmount: number;
  items: PurchaseOrderItem[];
}

export interface PurchaseOrderItem {
  id: number;
  purchaseOrderId: number;
  productId: number;
  product?: Product;
  quantity: number;
  unitCost: number;
  totalCost: number;
}

export interface CreatePurchaseOrderDto {
  supplierId: number;
  expectedDate?: string;
  notes?: string;
  items: CreatePurchaseOrderItemDto[];
}

export interface CreatePurchaseOrderItemDto {
  productId: number;
  quantity: number;
  unitCost: number;
}

export interface AnalyticsStats {
  total_stock_value: number;
  total_products: number;
  low_stock_count: number;
  stock_value_currency: string;
}

export interface SalesHistoryItem {
  month: string;
  total_revenue: number;
  total_orders: number;
}

export interface LowStockItem {
  id: number;
  product_name: string;
  product_code: string;
  current_stock: number;
  reorder_point: number;
  unit_cost: number;
}

export interface LowStockResponse {
  items: LowStockItem[];
}


export interface Location {
  id: number;
  warehouseId: number;
  code: string;
  name: string;
  zone?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateLocationDto {
  code: string;
  name: string;
  zone?: string;
  warehouseId: number;
}
