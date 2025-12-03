import { memo, useCallback } from 'react';
import {
  Box,
  Button,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  VStack,
  HStack,
  Text,
  useColorModeValue,
  Spinner,
  Center,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { invoicesApi } from '../services/api';

const Invoice = memo(() => {
  const { t } = useTranslation();
  const { orderId } = useParams<{ orderId: string }>();
  const bgColor = useColorModeValue('white', 'gray.800');

  const { data: invoice, isLoading } = useQuery({
    queryKey: ['invoice', orderId],
    queryFn: () => invoicesApi.getByOrderId(parseInt(orderId!)).then(res => res.data),
    enabled: !!orderId,
  });

  const handlePrint = useCallback(() => {
    window.print();
  }, []);

  if (isLoading) {
    return (
      <Center h="100vh">
        <Spinner size="xl" />
      </Center>
    );
  }

  if (!invoice) {
    return (
      <Center h="100vh">
        <Text>{t('errors.notFound')}</Text>
      </Center>
    );
  }

  return (
    <Box maxW="4xl" mx="auto" bg={bgColor} p={8} borderRadius="lg" boxShadow="md">
      <VStack spacing={6} align="stretch">
        <HStack justify="space-between">
          <Box>
            <Text fontSize="2xl" fontWeight="bold">
              {t('invoice.title')}
            </Text>
            <Text fontSize="sm" color="gray.600">
              {t('invoice.invoiceNumber')}: {invoice.invoiceNumber}
            </Text>
          </Box>
          <Button colorScheme="blue" onClick={handlePrint}>
            {t('invoice.print')}
          </Button>
        </HStack>

        <HStack justify="space-between" align="start">
          <Box>
            <Text fontWeight="bold">{t('invoice.customer')}</Text>
            <Text>{invoice.customerName}</Text>
            {invoice.customerEmail && <Text>{invoice.customerEmail}</Text>}
            {invoice.customerAddress && <Text>{invoice.customerAddress}</Text>}
          </Box>
          <Box textAlign="right">
            <Text>
              <strong>{t('invoice.orderNumber')}:</strong> {invoice.orderNumber}
            </Text>
            <Text>
              <strong>{t('invoice.invoiceDate')}:</strong>{' '}
              {new Date(invoice.invoiceDate).toLocaleDateString()}
            </Text>
            {invoice.dueDate && (
              <Text>
                <strong>{t('invoice.dueDate')}:</strong>{' '}
                {new Date(invoice.dueDate).toLocaleDateString()}
              </Text>
            )}
          </Box>
        </HStack>

        <Table variant="simple">
          <Thead>
            <Tr>
              <Th>{t('invoice.item')}</Th>
              <Th>{t('invoice.description')}</Th>
              <Th isNumeric>{t('invoice.qty')}</Th>
              <Th isNumeric>{t('invoice.price')}</Th>
              <Th isNumeric>{t('invoice.discount')}</Th>
              <Th isNumeric>{t('invoice.total')}</Th>
            </Tr>
          </Thead>
          <Tbody>
            {invoice.items.map((item) => (
              <Tr key={item.id}>
                <Td>{item.productCode}</Td>
                <Td>{item.productName}</Td>
                <Td isNumeric>{item.quantity}</Td>
                <Td isNumeric>{item.unitPrice.toFixed(2)} €</Td>
                <Td isNumeric>{item.discount.toFixed(2)} €</Td>
                <Td isNumeric>{item.lineTotal.toFixed(2)} €</Td>
              </Tr>
            ))}
          </Tbody>
        </Table>

        <HStack justify="flex-end" spacing={8}>
          <VStack align="end" spacing={2}>
            <Text>
              <strong>{t('invoice.subtotal')}:</strong> {invoice.subTotal.toFixed(2)} €
            </Text>
            <Text>
              <strong>{t('invoice.tax')} ({(invoice.taxRate * 100).toFixed(0)}%):</strong>{' '}
              {invoice.taxAmount.toFixed(2)} €
            </Text>
            <Text fontSize="xl" fontWeight="bold">
              <strong>{t('invoice.totalAmount')}:</strong> {invoice.totalAmount.toFixed(2)} €
            </Text>
          </VStack>
        </HStack>
      </VStack>
    </Box>
  );
});

Invoice.displayName = 'Invoice';

export default Invoice;

