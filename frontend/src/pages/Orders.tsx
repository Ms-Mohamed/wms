import { memo, useState, useCallback } from 'react';
import {
  Box,
  Button,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalCloseButton,
  useDisclosure,
  FormControl,
  FormLabel,
  Input,
  Select,
  NumberInput,
  NumberInputField,
  VStack,
  HStack,
  useToast,
  Badge,
  Link,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ordersApi, productsApi, warehousesApi, stockApi } from '../services/api';
import type { CreateOrderDto } from '../types';
import { useNavigate } from 'react-router-dom';

interface OrderItemForm {
  productId: number;
  warehouseId: number;
  quantity: number;
  unitPrice?: number;
  discount: number;
}

const Orders = memo(() => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { isOpen, onOpen, onClose } = useDisclosure();

  const [orderForm, setOrderForm] = useState({
    customerName: '',
    customerEmail: '',
    customerAddress: '',
    taxRate: 0.20,
  });

  const [orderItems, setOrderItems] = useState<OrderItemForm[]>([]);

  const { data: orders } = useQuery({
    queryKey: ['orders'],
    queryFn: () => ordersApi.getAll().then(res => res.data),
  });

  const { data: products } = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.getAll().then(res => res.data),
  });

  const { data: warehouses } = useQuery({
    queryKey: ['warehouses'],
    queryFn: () => warehousesApi.getAll().then(res => res.data),
  });

  const { data: stocks } = useQuery({
    queryKey: ['stocks'],
    queryFn: () => stockApi.getAll().then(res => res.data),
    enabled: isOpen,
  });

  const createOrderMutation = useMutation({
    mutationFn: (data: CreateOrderDto) => ordersApi.create(data).then(res => res.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['orders'] });
      toast({
        title: t('common.success'),
        description: 'Commande créée avec succès',
        status: 'success',
        duration: 3000,
      });
      onClose();
    },
    onError: (error: any) => {
      toast({
        title: t('errors.error'),
        description: error.response?.data?.error || t('errors.generic'),
        status: 'error',
        duration: 5000,
      });
    },
  });

  const handleAddItem = useCallback(() => {
    setOrderItems([...orderItems, { productId: 0, warehouseId: 0, quantity: 1, discount: 0 }]);
  }, [orderItems]);

  const handleItemChange = useCallback((index: number, field: keyof OrderItemForm, value: any) => {
    const newItems = [...orderItems];
    newItems[index] = { ...newItems[index], [field]: value };

    // Stock validation
    if (field === 'quantity' || field === 'productId' || field === 'warehouseId') {
      const item = newItems[index];
      if (item.productId && item.warehouseId) {
        const stock = stocks?.find(s => s.productId === item.productId && s.warehouseId === item.warehouseId);
        const max = stock ? (stock.availableQuantity ?? 0) : 0;
        if (item.quantity > max) {
          newItems[index].quantity = max;
        }
      }
    }
    setOrderItems(newItems);
  }, [orderItems, stocks]);

  const handleRemoveItem = useCallback((index: number) => {
    setOrderItems(orderItems.filter((_, i) => i !== index));
  }, [orderItems]);

  const handleSubmit = useCallback(() => {
    if (!orderForm.customerName || orderItems.length === 0) {
      toast({
        title: t('errors.error'),
        description: t('orders.fillRequiredFields'),
        status: 'error',
      });
      return;
    }

    const createOrderDto: CreateOrderDto = {
      customerName: orderForm.customerName,
      customerEmail: orderForm.customerEmail || undefined,
      customerAddress: orderForm.customerAddress || undefined,
      taxRate: orderForm.taxRate,
      items: orderItems.map(item => ({
        productId: item.productId,
        warehouseId: item.warehouseId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        discount: item.discount,
      })),
    };

    createOrderMutation.mutate(createOrderDto);
  }, [orderForm, orderItems, createOrderMutation, toast, t]);

  const getStatusBadge = useCallback((status: string) => {
    const colors: Record<string, string> = {
      pending: 'yellow',
      confirmed: 'blue',
      processing: 'purple',
      shipped: 'cyan',
      delivered: 'green',
      cancelled: 'red',
    };
    return (
      <Badge colorScheme={colors[status.toLowerCase()] || 'gray'}>
        {t(`orders.statuses.${status.toLowerCase()}`)}
      </Badge>
    );
  }, [t]);

  return (
    <Box>
      <Box mb={6} display="flex" justifyContent="space-between" alignItems="center">
        <h1 style={{ fontSize: '2rem', fontWeight: 'bold' }}>{t('orders.title')}</h1>
        <Button colorScheme="blue" onClick={onOpen}>
          {t('orders.createOrder')}
        </Button>
      </Box>

      <Box bg="white" borderRadius="lg" boxShadow="md" overflow="hidden">
        <Table variant="simple">
          <Thead bg="gray.50">
            <Tr>
              <Th>{t('orders.orderNumber')}</Th>
              <Th>{t('orders.customerName')}</Th>
              <Th>{t('orders.orderDate')}</Th>
              <Th>{t('orders.status')}</Th>
              <Th>{t('orders.total')}</Th>
              <Th>{t('common.actions')}</Th>
            </Tr>
          </Thead>
          <Tbody>
            {orders?.map((order) => (
              <Tr key={order.id}>
                <Td>{order.orderNumber}</Td>
                <Td>{order.customerName}</Td>
                <Td>{new Date(order.orderDate).toLocaleDateString()}</Td>
                <Td>{getStatusBadge(order.status)}</Td>
                <Td>{order.totalAmount.toFixed(2)} €</Td>
                <Td>
                  {['shipped', 'delivered'].includes(order.status.toLowerCase()) && (
                    <Link
                      color="blue.500"
                      onClick={() => navigate(`/invoices/${order.id}`)}
                      cursor="pointer"
                    >
                      {t('orders.viewInvoice')}
                    </Link>
                  )}
                </Td>
              </Tr>
            ))}
          </Tbody>
        </Table>
      </Box>

      <Modal isOpen={isOpen} onClose={onClose} size="xl">
        <ModalOverlay />
        <ModalContent>
          <ModalHeader>{t('orders.createOrder')}</ModalHeader>
          <ModalCloseButton />
          <ModalBody pb={6}>
            <VStack spacing={4}>
              <FormControl>
                <FormLabel>{t('orders.customerName')}</FormLabel>
                <Input
                  value={orderForm.customerName}
                  onChange={(e) => setOrderForm({ ...orderForm, customerName: e.target.value })}
                />
              </FormControl>

              <FormControl>
                <FormLabel>{t('orders.customerEmail')}</FormLabel>
                <Input
                  type="email"
                  value={orderForm.customerEmail}
                  onChange={(e) => setOrderForm({ ...orderForm, customerEmail: e.target.value })}
                />
              </FormControl>

              <FormControl>
                <FormLabel>{t('orders.customerAddress')}</FormLabel>
                <Input
                  value={orderForm.customerAddress}
                  onChange={(e) => setOrderForm({ ...orderForm, customerAddress: e.target.value })}
                />
              </FormControl>

              <Box w="100%">
                <Button size="sm" onClick={handleAddItem} mb={4}>
                  {t('orders.addItem')}
                </Button>

                {orderItems.map((item, index) => (
                  <Box key={index} p={4} border="1px" borderColor="gray.200" borderRadius="md" mb={2}>
                    <HStack spacing={4}>
                      <FormControl flex="1">
                        <FormLabel>{t('orders.product')}</FormLabel>
                        <Select
                          value={item.productId}
                          onChange={(e) => handleItemChange(index, 'productId', parseInt(e.target.value))}
                        >
                          <option value={0}>{t('orders.selectProduct')}</option>
                          {products?.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.code} - {p.name}
                            </option>
                          ))}
                        </Select>
                      </FormControl>

                      <FormControl flex="1">
                        <FormLabel>{t('stock.warehouse')}</FormLabel>
                        <Select
                          value={item.warehouseId}
                          onChange={(e) => handleItemChange(index, 'warehouseId', parseInt(e.target.value))}
                        >
                          <option value={0}>{t('orders.selectWarehouse')}</option>
                          {warehouses?.map((w) => (
                            <option key={w.id} value={w.id}>
                              {w.name}
                            </option>
                          ))}
                        </Select>
                      </FormControl>

                      <FormControl w="120px">
                        <FormLabel>
                          {t('orders.quantity')}
                          {item.productId && item.warehouseId && (
                            <span style={{ fontSize: '0.7em', color: 'gray', marginLeft: '4px' }}>
                              (max: {stocks?.find(s => s.productId === item.productId && s.warehouseId === item.warehouseId)?.availableQuantity ?? 0})
                            </span>
                          )}
                        </FormLabel>
                        <NumberInput
                          value={item.quantity}
                          onChange={(_, val) => handleItemChange(index, 'quantity', val)}
                          min={1}
                          max={stocks?.find(s => s.productId === item.productId && s.warehouseId === item.warehouseId)?.availableQuantity ?? 9999}
                        >
                          <NumberInputField />
                        </NumberInput>
                      </FormControl>

                      <Button
                        size="sm"
                        colorScheme="red"
                        onClick={() => handleRemoveItem(index)}
                      >
                        {t('common.delete')}
                      </Button>
                    </HStack>
                  </Box>
                ))}
              </Box>

              <HStack spacing={4} w="100%">
                <Button onClick={onClose}>{t('common.cancel')}</Button>
                <Button
                  colorScheme="blue"
                  onClick={handleSubmit}
                  isLoading={createOrderMutation.isPending}
                >
                  {t('orders.create')}
                </Button>
              </HStack>
            </VStack>
          </ModalBody>
        </ModalContent>
      </Modal>
    </Box>
  );
});

Orders.displayName = 'Orders';

export default Orders;

