import { memo, useMemo } from 'react';
import { Box, Grid, Stat, StatLabel, StatNumber, StatHelpText, useColorModeValue } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { ordersApi, productsApi, stockApi } from '../services/api';

const Dashboard = memo(() => {
  const { t } = useTranslation();
  const bgColor = useColorModeValue('white', 'gray.800');

  const { data: orders } = useQuery({
    queryKey: ['orders'],
    queryFn: () => ordersApi.getAll().then(res => res.data),
  });

  const { data: products } = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.getAll().then(res => res.data),
  });

  const { data: stocks } = useQuery({
    queryKey: ['stocks'],
    queryFn: () => stockApi.getAll().then(res => res.data),
  });

  const stats = useMemo(() => {
    const totalOrders = orders?.length || 0;
    const totalProducts = products?.length || 0;
    const totalStockValue = stocks?.reduce((sum, stock) => sum + (stock.quantity * stock.averageCost), 0) || 0;
    const lowStockItems = stocks?.filter(stock => stock.quantity <= stock.reorderPoint).length || 0;

    return {
      totalOrders,
      totalProducts,
      totalStockValue: totalStockValue.toFixed(2),
      lowStockItems,
    };
  }, [orders, products, stocks]);

  return (
    <Box>
      <Box mb={6}>
        <h1 style={{ fontSize: '2rem', fontWeight: 'bold', marginBottom: '1rem' }}>
          {t('nav.dashboard')}
        </h1>
      </Box>

      <Grid templateColumns="repeat(auto-fit, minmax(250px, 1fr))" gap={6} mb={6}>
        <Stat bg={bgColor} p={6} borderRadius="lg" boxShadow="md">
          <StatLabel>{t('nav.orders')}</StatLabel>
          <StatNumber>{stats.totalOrders}</StatNumber>
          <StatHelpText>{t('dashboard.totalOrders')}</StatHelpText>
        </Stat>

        <Stat bg={bgColor} p={6} borderRadius="lg" boxShadow="md">
          <StatLabel>{t('nav.products')}</StatLabel>
          <StatNumber>{stats.totalProducts}</StatNumber>
          <StatHelpText>{t('dashboard.totalProducts')}</StatHelpText>
        </Stat>

        <Stat bg={bgColor} p={6} borderRadius="lg" boxShadow="md">
          <StatLabel>{t('dashboard.stockValue')}</StatLabel>
          <StatNumber>{stats.totalStockValue} €</StatNumber>
          <StatHelpText>{t('dashboard.totalValue')}</StatHelpText>
        </Stat>

        <Stat bg={bgColor} p={6} borderRadius="lg" boxShadow="md">
          <StatLabel>{t('dashboard.stockAlerts')}</StatLabel>
          <StatNumber color={stats.lowStockItems > 0 ? 'red.500' : 'green.500'}>
            {stats.lowStockItems}
          </StatNumber>
          <StatHelpText>{t('dashboard.productsToReorder')}</StatHelpText>
        </Stat>
      </Grid>
    </Box>
  );
});

Dashboard.displayName = 'Dashboard';

export default Dashboard;

