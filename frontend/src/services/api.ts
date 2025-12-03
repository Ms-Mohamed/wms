import axios from 'axios';
import type {
  Product,
  Order,
  OrderItem,
  CreateOrderDto,
  CreateOrderItemDto,
  Invoice,
  InvoiceItem,
  Stock,
  Warehouse,
  DemandForecast,
  Optimization,
} from '../types';

const API_BASE_URL = '/api';
const ANALYTICS_BASE_URL = '/analytics';

// API C# (Port 5000)
export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// API Python (Port 8000)
export const analyticsClient = axios.create({
  baseURL: ANALYTICS_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// API Functions - Products
export const productsApi = {
  getAll: () => apiClient.get<Product[]>('/products'),
  getById: (id: number) => apiClient.get<Product>(`/products/${id}`),
  create: (data: Partial<Product>) => apiClient.post<Product>('/products', data),
  update: (id: number, data: Partial<Product>) => apiClient.put<Product>(`/products/${id}`, data),
  delete: (id: number) => apiClient.delete(`/products/${id}`),
};

// API Functions - Orders
export const ordersApi = {
  getAll: () => apiClient.get<Order[]>('/orders'),
  getById: (id: number) => apiClient.get<Order>(`/orders/${id}`),
  create: (data: CreateOrderDto) => apiClient.post<Order>('/orders', data),
};

// API Functions - Invoices
export const invoicesApi = {
  getByOrderId: (orderId: number) => apiClient.get<Invoice>(`/invoices/${orderId}`),
  getById: (id: number) => apiClient.get<Invoice>(`/invoices/id/${id}`),
};

// API Functions - Stock
export const stockApi = {
  getAll: () => apiClient.get<Stock[]>('/stocks'),
  getByProductAndWarehouse: (productId: number, warehouseId: number) =>
    apiClient.get<Stock>(`/stocks/product/${productId}/warehouse/${warehouseId}`),
};

// API Functions - Warehouses
export const warehousesApi = {
  getAll: () => apiClient.get<Warehouse[]>('/warehouses'),
};

// Analytics API Functions
export const analyticsApi = {
  predict: (productId: number) => analyticsClient.get<DemandForecast>(`/predict/${productId}`),
  optimize: (productId: number) => analyticsClient.get<Optimization>(`/optimize/${productId}`),
};

// Re-export types for convenience
export type {
  Product,
  Order,
  OrderItem,
  CreateOrderDto,
  CreateOrderItemDto,
  Invoice,
  InvoiceItem,
  Stock,
  Warehouse,
  DemandForecast,
  Optimization,
} from '../types';

