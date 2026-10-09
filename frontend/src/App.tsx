import { Suspense, lazy } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Spinner, Center } from '@chakra-ui/react';
import Layout from './components/Layout/Layout';

// Code Splitting avec React.lazy
const Dashboard = lazy(() => import('./pages/Dashboard'));
const Orders = lazy(() => import('./pages/Orders'));
const Products = lazy(() => import('./pages/Products'));
const Stock = lazy(() => import('./pages/Stock'));
const Analytics = lazy(() => import('./pages/Analytics'));
const Invoice = lazy(() => import('./pages/Invoice'));
const Suppliers = lazy(() => import('./pages/Suppliers'));
const Locations = lazy(() => import('./pages/Locations')); // [NEW]
const PurchaseOrders = lazy(() => import('./pages/PurchaseOrders'));
const Returns = lazy(() => import('./pages/Returns')); // [NEW]

// Loading component
const LoadingSpinner = () => (
  <Center h="100vh">
    <Spinner size="xl" color="brand.500" />
  </Center>
);

import { AuthProvider } from './context/AuthContext';
import Login from './pages/Login';
import PrivateRoute from './components/PrivateRoute';

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Suspense fallback={<LoadingSpinner />}>
          <Routes>
            <Route path="/login" element={<Login />} />

            <Route element={<PrivateRoute />}>
              <Route element={<Layout />}>
                <Route path="/" element={<Navigate to="/dashboard" replace />} />
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/orders" element={<Orders />} />
                <Route path="/sales-orders" element={<Navigate to="/orders" replace />} />
                <Route path="/returns" element={<Returns />} /> {/* [NEW] */}
                <Route path="/purchase-orders" element={<PurchaseOrders />} /> {/* [NEW] */}
                <Route path="/products" element={<Products />} />
                <Route path="/stock" element={<Stock />} />
                <Route path="/suppliers" element={<Suppliers />} /> {/* [NEW] */}
                <Route path="/locations" element={<Locations />} /> {/* [NEW] */}
                <Route path="/analytics" element={<Analytics />} />
                <Route path="/invoices/:orderId" element={<Invoice />} />
              </Route>
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
