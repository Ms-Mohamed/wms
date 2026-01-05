import { memo, useMemo } from 'react';
import {
  Box,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  Badge,
  useColorModeValue,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { stockApi } from '../services/api';

const Stock = memo(() => {
  const { t } = useTranslation();
  const bgColor = useColorModeValue('white', 'gray.800');

  const { data: stocks } = useQuery({
    queryKey: ['stocks'],
    queryFn: () => stockApi.getAll().then(res => res.data),
  });

  const getStockStatus = useMemo(() => {
    return (stock: any) => {
      if (stock.quantity <= stock.reorderPoint) {
        return { color: 'red', label: t('stock.status.critical') };
      }
      if (stock.quantity <= stock.reorderPoint * 1.5) {
        return { color: 'yellow', label: t('stock.status.warning') };
      }
      return { color: 'green', label: t('stock.status.normal') };
    };
  }, [t]);

  return (
    <Box>
      <Box mb={6}>
        <h1 style={{ fontSize: '2rem', fontWeight: 'bold' }}>{t('stock.title')}</h1>
      </Box>

      <Box bg={bgColor} borderRadius="lg" boxShadow="md" overflow="hidden">
        <Table variant="simple">
          <Thead bg="gray.50">
            <Tr>
              <Th>{t('products.code')}</Th>
              <Th>{t('products.name')}</Th>
              <Th>{t('stock.warehouse')}</Th>
              <Th>Emplacement</Th>
              <Th>{t('stock.available')}</Th>
              <Th>{t('stock.reserved')}</Th>
              <Th>{t('stock.reorderPoint')}</Th>
              <Th>{t('stock.status.label')}</Th>
            </Tr>
          </Thead>
          <Tbody>
            {stocks?.map((stock) => {
              const status = getStockStatus(stock);
              return (
                <Tr key={stock.id}>
                  <Td>{stock.productCode}</Td>
                  <Td>{stock.productName}</Td>
                  <Td>{stock.warehouseName}</Td>
                  <Td>{stock.locationName || '-'}</Td>
                  <Td>{stock.availableQuantity.toFixed(2)}</Td>
                  <Td>{stock.reservedQuantity.toFixed(2)}</Td>
                  <Td>{stock.reorderPoint.toFixed(2)}</Td>
                  <Td>
                    <Badge colorScheme={status.color}>{status.label}</Badge>
                  </Td>
                </Tr>
              );
            })}
          </Tbody>
        </Table>
      </Box>
    </Box>
  );
});

Stock.displayName = 'Stock';

export default Stock;

