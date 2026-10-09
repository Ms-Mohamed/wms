import { memo } from 'react';
import { Box, Flex, SimpleGrid, Table, Tbody, Td, Text, Th, Thead, Tr } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { analyticsApi, ordersApi } from '../services/api';
import Card from '../components/ui/Card';
import EmptyState from '../components/ui/EmptyState';
import PageHeader from '../components/ui/PageHeader';
import StatusBadge from '../components/ui/StatusBadge';

function Kpi({ label, value, tone = 'ink.800', hint }: { label: string; value: string | number; tone?: string; hint?: string }) {
  return (
    <Card p={5}>
      <Text fontSize="xs" fontWeight={600} color="ink.500">{label}</Text>
      <Text mt={1} fontSize="3xl" fontWeight={700} letterSpacing="-0.03em" color={tone} lineHeight={1.1}>{value}</Text>
      {hint && <Text mt={1} fontSize="xs" color="ink.400">{hint}</Text>}
    </Card>
  );
}

const Dashboard = memo(() => {
  const { t } = useTranslation();
  const navigate = useNavigate();

  // Server-side aggregates and one page of rows: the dashboard never downloads a whole table.
  const { data: recent } = useQuery({
    queryKey: ['orders', 'recent'],
    queryFn: () => ordersApi.getAll({ page: 1, pageSize: 6 }).then((r) => ({ rows: r.data, total: Number(r.headers['x-total-count'] ?? r.data.length) })),
  });
  const { data: stats } = useQuery({ queryKey: ['analytics', 'stats'], queryFn: () => analyticsApi.getStats().then((r) => r.data) });
  const { data: low } = useQuery({ queryKey: ['analytics', 'low-stock'], queryFn: () => analyticsApi.getLowStock().then((r) => r.data.items.slice(0, 6)) });

  const lowCount = stats?.low_stock_count ?? 0;

  return (
    <Box>
      <PageHeader title={t('nav.dashboard')} />
      <SimpleGrid columns={{ base: 1, sm: 2, xl: 4 }} spacing={4} mb={6}>
        <Kpi label={t('dashboard.totalOrders')} value={recent?.total ?? 0} />
        <Kpi label={t('dashboard.totalProducts')} value={stats?.total_products ?? 0} />
        <Kpi label={t('dashboard.stockValue')} value={`${(stats?.total_stock_value ?? 0).toLocaleString(undefined, { maximumFractionDigits: 0 })} €`} />
        <Kpi label={t('dashboard.stockAlerts')} value={lowCount} tone={lowCount > 0 ? 'red.500' : 'green.500'} hint={t('dashboard.productsToReorder')} />
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 5 }} spacing={4}>
        <Card gridColumn={{ xl: 'span 3' }}>
          <Flex px={5} py={4} justify="space-between" align="center">
            <Text fontWeight={600}>{t('dashboard.recentOrders')}</Text>
            <Text as="button" fontSize="sm" color="brand.600" fontWeight={600} onClick={() => navigate('/orders')}>{t('nav.orders')} →</Text>
          </Flex>
          <Box overflowX="auto">
            <Table>
              <Thead><Tr><Th>{t('orders.orderNumber')}</Th><Th>{t('orders.customerName')}</Th><Th>{t('orders.status')}</Th><Th isNumeric>{t('orders.total')}</Th></Tr></Thead>
              <Tbody>
                {recent?.rows.map((o) => (
                  <Tr key={o.id} cursor="pointer" onClick={() => navigate('/orders')}>
                    <Td fontWeight={600}>{o.orderNumber}</Td><Td>{o.customerName}</Td><Td><StatusBadge status={o.status} /></Td>
                    <Td isNumeric>{o.totalAmount.toFixed(2)} €</Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
          {recent && recent.rows.length === 0 && <EmptyState>{t('orders.noOrders')}</EmptyState>}
        </Card>

        <Card gridColumn={{ xl: 'span 2' }}>
          <Flex px={5} py={4}><Text fontWeight={600}>{t('dashboard.lowStock')}</Text></Flex>
          {low && low.length === 0 && <EmptyState>{t('dashboard.allGood')}</EmptyState>}
          {low?.map((i) => (
            <Flex key={i.id} px={5} py={3} borderTop="1px solid" borderColor="ink.100" justify="space-between" align="center">
              <Box minW={0}><Text fontSize="sm" fontWeight={600} noOfLines={1}>{i.product_name}</Text><Text fontSize="xs" color="ink.400">{i.product_code}</Text></Box>
              <Text fontSize="sm" color="red.500" fontWeight={700}>{i.current_stock} <Text as="span" color="ink.400" fontWeight={400}>/ {i.reorder_point}</Text></Text>
            </Flex>
          ))}
        </Card>
      </SimpleGrid>
    </Box>
  );
});

Dashboard.displayName = 'Dashboard';
export default Dashboard;
