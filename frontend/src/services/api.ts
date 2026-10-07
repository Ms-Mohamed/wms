import axios from 'axios';
import type {
  Product,
  Order,
  Invoice,
  Stock,
  Warehouse,
  DemandForecast,
  Optimization,
  Supplier,
  PurchaseOrder,
  CreatePurchaseOrderDto,
  CreateLocationDto,
  AnalyticsStats,
  SalesHistoryItem,
  LowStockResponse,
  Location,
} from '../types';

const API_BASE_URL = '/api';
const ANALYTICS_BASE_URL = '/python-api';

// API C# (Port 5000)
export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Interceptor pour ajouter le token JWT à chaque requête
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Interceptor pour gérer les erreurs (ex: 401 Unauthorized)
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response && error.response.status === 401) {
      // Token expiré ou invalide
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

// API Python (Port 8000)
export const analyticsClient = axios.create({
  baseURL: ANALYTICS_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});
// The analytics service validates the same JWT as the C# API.
analyticsClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
analyticsClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response && error.response.status === 401) {
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

// API Functions - Products
export const productsApi = {
  getAll: () => apiClient.get<Product[]>('/products'),
  getById: (id: number) => apiClient.get<Product>(`/products/${id}`),
  create: (data: Partial<Product>) => apiClient.post<Product>('/products', data),
  update: (id: number, data: Partial<Product>) => apiClient.put<Product>(`/products/${id}`, data),
  delete: (id: number) => apiClient.delete(`/products/${id}`),
};

// API Functions - Orders


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
  Supplier,
  PurchaseOrder,
  CreatePurchaseOrderDto,
  AnalyticsStats,
  SalesHistoryItem,
  LowStockResponse,
} from '../types';

// API Functions - Suppliers
export const suppliersApi = {
  getAll: () => apiClient.get<Supplier[]>('/suppliers'),
  getById: (id: number) => apiClient.get<Supplier>(`/suppliers/${id}`),
};

// API Functions - Purchase Orders
export const purchaseOrdersApi = {
  getAll: () => apiClient.get<PurchaseOrder[]>('/purchaseorders'),
  getById: (id: number) => apiClient.get<PurchaseOrder>(`/purchaseorders/${id}`),
  create: (data: CreatePurchaseOrderDto) => apiClient.post<PurchaseOrder>('/purchaseorders', data),
  receive: (id: number, locationId?: number) => apiClient.post(`/purchaseorders/${id}/receive${locationId ? `?locationId=${locationId}` : ''}`),
};

export const ordersApi = {
  getAll: (params?: { page?: number; pageSize?: number }) => apiClient.get<Order[]>('/orders', { params }),
  create: (data: any) => apiClient.post<Order>('/orders', data),
  ship: (id: number, data: { items: { orderItemId: number; locationId: number; quantity: number }[] }) => apiClient.post<Order>(`/orders/${id}/ship`, data),
  get: (id: number) => apiClient.get<Order>(`/orders/${id}`),
};

export const locationsApi = {
  getAll: () => apiClient.get<Location[]>('/locations'),
  create: (data: CreateLocationDto) => apiClient.post<Location>('/locations', data),
  update: (id: number, data: CreateLocationDto) => apiClient.put<Location>(`/locations/${id}`, data),
  delete: (id: number) => apiClient.delete(`/locations/${id}`),
};

// Analytics API Functions
export const analyticsApi = {
  predict: (productId: number) => analyticsClient.get<DemandForecast>(`/predict/${productId}`),
  optimize: (productId: number) => analyticsClient.get<Optimization>(`/optimize/${productId}`),
  getStats: () => analyticsClient.get<AnalyticsStats>('/stats'),
  getSalesHistory: () => analyticsClient.get<SalesHistoryItem[]>('/sales-history'),
  getLowStock: () => analyticsClient.get<LowStockResponse>('/low-stock'),
};

