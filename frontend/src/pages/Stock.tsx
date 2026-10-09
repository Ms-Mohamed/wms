import { memo } from 'react';
import { Badge, Box, Table, Tbody, Td, Th, Thead, Tr, Text, Tooltip } from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { stockApi } from '../services/api';
import type { Stock as StockRow } from '../types';
import { usePaged } from '../hooks/usePaged';
import Card from '../components/ui/Card';
import EmptyState from '../components/ui/EmptyState';
import PageHeader from '../components/ui/PageHeader';
import Pagination from '../components/ui/Pagination';

const fmt = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 3 });

/** On-hand split into reserved (held for an order) and free. */
function Bar({ row }: { row: StockRow }) {
  const total = row.quantity || 0;
  const reservedPct = total > 0 ? Math.min(100, (row.reservedQuantity / total) * 100) : 0;
  return (
    <Tooltip hasArrow label={`${fmt(row.reservedQuantity)} reserved · ${fmt(row.availableQuantity)} free`}>
      <Box w="96px" h="6px" borderRadius="full" bg="green.200" overflow="hidden">
        <Box h="100%" w={`${reservedPct}%`} bg="brand.500" />
      </Box>
    </Tooltip>
  );
}

const Stock = memo(() => {
  const { t } = useTranslation();
  const paged = usePaged<StockRow>('stocks', (p) => stockApi.getPage(p));

  const status = (s: StockRow) => {
    if (s.availableQuantity <= s.reorderPoint) return { color: 'red', label: t('stock.status.critical') };
    if (s.availableQuantity <= s.reorderPoint * 1.5) return { color: 'orange', label: t('stock.status.warning') };
    return { color: 'green', label: t('stock.status.normal') };
  };

  return (
    <Box>
      <PageHeader title={t('stock.title')} subtitle={`${paged.total}`} />
      <Card>
        <Box overflowX="auto" opacity={paged.isPlaceholderData ? 0.6 : 1} transition="opacity .15s">
          <Table>
            <Thead>
              <Tr>
                <Th>{t('products.code')}</Th><Th>{t('products.name')}</Th><Th>{t('stock.warehouse')}</Th><Th>{t('stock.location')}</Th>
                <Th isNumeric>{t('stock.onHand')}</Th><Th isNumeric>{t('stock.reserved')}</Th><Th isNumeric>{t('stock.free')}</Th>
                <Th />
                <Th>{t('stock.status.label')}</Th>
              </Tr>
            </Thead>
            <Tbody>
              {paged.rows.map((s) => {
                const st = status(s);
                return (
                  <Tr key={s.id}>
                    <Td fontWeight={600}>{s.productCode}</Td>
                    <Td>{s.productName}</Td>
                    <Td color="ink.500">{s.warehouseName}</Td>
                    <Td color="ink.500">{s.locationName || '—'}</Td>
                    <Td isNumeric>{fmt(s.quantity)}</Td>
                    <Td isNumeric><Text color={s.reservedQuantity > 0 ? 'brand.600' : 'ink.400'}>{fmt(s.reservedQuantity)}</Text></Td>
                    <Td isNumeric fontWeight={600}>{fmt(s.availableQuantity)}</Td>
                    <Td><Bar row={s} /></Td>
                    <Td><Badge colorScheme={st.color} variant="subtle" borderRadius="full" px={2.5} textTransform="none">{st.label}</Badge></Td>
                  </Tr>
                );
              })}
            </Tbody>
          </Table>
        </Box>
        {!paged.isLoading && paged.rows.length === 0 && <EmptyState>{t('stock.empty')}</EmptyState>}
        <Pagination page={paged.page} pageCount={paged.pageCount} pageSize={paged.pageSize} total={paged.total} onPage={paged.setPage} onPageSize={paged.setPageSize} />
      </Card>
    </Box>
  );
});

Stock.displayName = 'Stock';
export default Stock;
