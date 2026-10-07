import { memo, useMemo } from 'react';
import { Box, Grid, Stat, StatLabel, StatNumber, StatHelpText, useColorModeValue } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { analyticsApi, ordersApi } from '../services/api';

const Dashboard = memo(() => {
  const { t } = useTranslation();
  const bgColor = useColorModeValue('white', 'gray.800');

  // Server-side aggregates: the dashboard no longer downloads every row just to count it.
  const { data: ordersTotal } = useQuery({
    queryKey: ['orders', 'total'],
    queryFn: () =>
      ordersApi.getAll({ page: 1, pageSize: 1 }).then(res => Number(res.headers['x-total-count'] ?? 0)),
  });

  const { data: analytics } = useQuery({
    queryKey: ['analytics', 'stats'],
    queryFn: () => analyticsApi.getStats().then(res => res.data),
  });

  const stats = useMemo(
    () => ({
      totalOrders: ordersTotal ?? 0,
      totalProducts: analytics?.total_products ?? 0,
      totalStockValue: (analytics?.total_stock_value ?? 0).toFixed(2),
      lowStockItems: analytics?.low_stock_count ?? 0,
    }),
    [ordersTotal, analytics],
  );

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

