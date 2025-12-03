// Types pour les entités de l'API

export interface Product {
  id: number;
  code: string;
  name: string;
  description: string;
  unitPrice: number;
  costPrice: number;
  unit: string;
  stockQuantity?: number;
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

