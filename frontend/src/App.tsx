import { Suspense, lazy } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Box, Spinner, Center } from '@chakra-ui/react';
import Layout from './components/Layout/Layout';

// Code Splitting avec React.lazy
const Dashboard = lazy(() => import('./pages/Dashboard'));
const Orders = lazy(() => import('./pages/Orders'));
const Products = lazy(() => import('./pages/Products'));
const Stock = lazy(() => import('./pages/Stock'));
const Analytics = lazy(() => import('./pages/Analytics'));
const Invoice = lazy(() => import('./pages/Invoice'));

// Loading component
const LoadingSpinner = () => (
  <Center h="100vh">
    <Spinner size="xl" color="blue.500" />
  </Center>
);

function App() {
  return (
    <BrowserRouter>
      <Layout>
        <Suspense fallback={<LoadingSpinner />}>
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/orders" element={<Orders />} />
            <Route path="/products" element={<Products />} />
            <Route path="/stock" element={<Stock />} />
            <Route path="/analytics" element={<Analytics />} />
            <Route path="/invoices/:orderId" element={<Invoice />} />
          </Routes>
        </Suspense>
      </Layout>
    </BrowserRouter>
  );
}

export default App;
