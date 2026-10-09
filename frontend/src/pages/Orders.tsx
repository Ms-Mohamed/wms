import { memo, useMemo, useState } from 'react';
import {
  Box, Button, Drawer, DrawerBody, DrawerCloseButton, DrawerContent, DrawerFooter, DrawerHeader, DrawerOverlay,
  Flex, FormControl, FormLabel, HStack, IconButton, Input, Modal, ModalBody, ModalCloseButton, ModalContent,
  ModalFooter, ModalHeader, ModalOverlay, NumberInput, NumberInputField, Progress, Select, Table, Tbody, Td,
  Text, Th, Thead, Tr, useDisclosure, useToast, VStack,
} from '@chakra-ui/react';
import { PlusIcon, TrashIcon } from '@heroicons/react/24/outline';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ordersApi, productsApi, stockApi, warehousesApi } from '../services/api';
import type { CreateOrderDto, Order, OrderItem, Stock } from '../types';
import { usePaged } from '../hooks/usePaged';
import Card from '../components/ui/Card';
import EmptyState from '../components/ui/EmptyState';
import PageHeader from '../components/ui/PageHeader';
import Pagination from '../components/ui/Pagination';
import StatusBadge from '../components/ui/StatusBadge';

const num = (n: number) => Number(n.toLocaleString('en', { maximumFractionDigits: 3 }).replace(/,/g, ''));
const money = (n: number) => `${n.toFixed(2)} €`;
const errorText = (e: any, fallback: string): string => e?.response?.data?.error ?? e?.response?.data?.message ?? fallback;

const CLOSED = ['cancelled', 'delivered'];
const canReserve = (o: Order) => ['pending', 'confirmed', 'processing', 'partiallyshipped'].includes(o.status.toLowerCase())
  && o.items.some((i) => i.reservedQuantity + i.shippedQuantity < i.quantity);
const canShip = (o: Order) => ['pending', 'confirmed', 'processing', 'partiallyshipped'].includes(o.status.toLowerCase());
const canCancel = (o: Order) => !CLOSED.includes(o.status.toLowerCase()) && o.status.toLowerCase() !== 'shipped'
  && o.status.toLowerCase() !== 'partiallyshipped' && o.items.every((i) => i.shippedQuantity === 0);

function Fulfilment({ order }: { order: Order }) {
  const ordered = order.items.reduce((s, i) => s + i.quantity, 0);
  const shipped = order.items.reduce((s, i) => s + i.shippedQuantity, 0);
  const pct = ordered ? (shipped / ordered) * 100 : 0;
  return (
    <Box minW="120px">
      <Progress value={pct} size="xs" borderRadius="full" colorScheme={pct >= 100 ? 'green' : 'brand'} bg="ink.100" />
      <Text mt={1} fontSize="xs" color="ink.500">{num(shipped)} / {num(ordered)}</Text>
    </Box>
  );
}

/* ---------------------------- detail drawer ---------------------------- */

function OrderDrawer({ orderId, onClose }: { orderId: number | null; onClose: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [qty, setQty] = useState<Record<number, number>>({});
  const [from, setFrom] = useState<Record<number, number>>({});

  const { data: order } = useQuery({
    queryKey: ['orders', 'one', orderId],
    queryFn: () => ordersApi.get(orderId!).then((r) => r.data),
    enabled: orderId !== null,
  });
  const { data: stocks } = useQuery({
    queryKey: ['stocks'],
    queryFn: () => stockApi.getAll().then((r) => r.data),
    enabled: orderId !== null,
  });

  const refresh = (o: Order) => {
    qc.setQueryData(['orders', 'one', o.id], o);
    qc.invalidateQueries({ queryKey: ['orders'] });
    qc.invalidateQueries({ queryKey: ['stocks'] });
    setQty({});
  };

  const reserve = useMutation({
    mutationFn: () => ordersApi.reserve(orderId!).then((r) => r.data),
    onSuccess: (o) => { refresh(o); toast({ title: t('orders.reserved_ok'), status: 'success' }); },
    onError: (e: any) => {
      const code = e?.response?.data?.productCode;
      toast({ title: t('errors.error'), description: code ? t('orders.shortage', { product: code }) : errorText(e, t('errors.generic')), status: 'error', duration: 6000 });
    },
  });
  const cancel = useMutation({
    mutationFn: () => ordersApi.cancel(orderId!).then((r) => r.data),
    onSuccess: (o) => { refresh(o); toast({ title: t('orders.cancelled_ok'), status: 'success' }); },
    onError: (e: any) => toast({ title: t('errors.error'), description: errorText(e, t('errors.generic')), status: 'error' }),
  });
  const ship = useMutation({
    mutationFn: (items: { orderItemId: number; locationId: number; quantity: number }[]) => ordersApi.ship(orderId!, { items }).then((r) => r.data),
    onSuccess: (o) => { refresh(o); toast({ title: t('orders.shipped_ok'), status: 'success' }); },
    onError: (e: any) => toast({ title: t('errors.error'), description: errorText(e, t('errors.generic')), status: 'error', duration: 6000 }),
  });

  const rowsFor = (item: OrderItem): Stock[] =>
    (stocks ?? []).filter((s) => s.productId === item.productId && s.warehouseId === item.warehouseId);
  const remaining = (i: OrderItem) => Math.max(0, i.quantity - i.shippedQuantity);
  const chosen = (i: OrderItem) => from[i.id] ?? rowsFor(i)[0]?.id ?? 0;
  const amount = (i: OrderItem) => qty[i.id] ?? remaining(i);

  const shipment = useMemo(() => {
    if (!order) return [];
    return order.items
      .filter((i) => remaining(i) > 0 && amount(i) > 0 && chosen(i) > 0)
      .map((i) => ({ orderItemId: i.id, locationId: chosen(i), quantity: amount(i) }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [order, qty, from, stocks]);

  const busy = reserve.isPending || cancel.isPending || ship.isPending;

  return (
    <Drawer isOpen={orderId !== null} onClose={onClose} size="lg" placement="right">
      <DrawerOverlay />
      <DrawerContent>
        <DrawerCloseButton />
        <DrawerHeader borderBottom="1px solid" borderColor="ink.100">
          {order ? (
            <VStack align="start" spacing={1}>
              <Flex gap={3} align="center"><Text fontSize="lg">{order.orderNumber}</Text><StatusBadge status={order.status} /></Flex>
              <Text fontSize="sm" fontWeight={400} color="ink.500">{order.customerName} · {new Date(order.orderDate).toLocaleDateString()}</Text>
            </VStack>
          ) : t('orders.details')}
        </DrawerHeader>
        <DrawerBody py={5}>
          {order && (
            <VStack align="stretch" spacing={4}>
              {order.status.toLowerCase() === 'partiallyshipped' && (
                <Box bg="yellow.50" color="yellow.800" fontSize="sm" borderRadius="md" px={3} py={2}>{t('orders.partialNote')}</Box>
              )}
              {order.items.map((item) => {
                const rem = remaining(item);
                const rows = rowsFor(item);
                return (
                  <Box key={item.id} borderRadius="lg" boxShadow="card" p={4}>
                    <Flex justify="space-between" gap={3}>
                      <Box minW={0}>
                        <Text fontWeight={600} noOfLines={1}>{item.productName}</Text>
                        <Text fontSize="xs" color="ink.500">{item.productCode} · {item.warehouseName}</Text>
                      </Box>
                      <Text fontSize="sm" fontWeight={600}>{money(item.lineTotal)}</Text>
                    </Flex>
                    <HStack mt={3} spacing={6} fontSize="xs" color="ink.500">
                      <Box><Text>{t('orders.ordered')}</Text><Text fontSize="md" color="ink.800" fontWeight={600}>{num(item.quantity)}</Text></Box>
                      <Box><Text>{t('orders.reservedQty')}</Text><Text fontSize="md" color="brand.600" fontWeight={600}>{num(item.reservedQuantity)}</Text></Box>
                      <Box><Text>{t('orders.shippedQty')}</Text><Text fontSize="md" color="green.600" fontWeight={600}>{num(item.shippedQuantity)}</Text></Box>
                      <Box><Text>{t('orders.remaining')}</Text><Text fontSize="md" color="ink.800" fontWeight={600}>{num(rem)}</Text></Box>
                    </HStack>
                    {canShip(order) && rem > 0 && (
                      <HStack mt={4} spacing={3} align="flex-end">
                        <FormControl flex="1">
                          <FormLabel>{t('orders.pickFrom')}</FormLabel>
                          <Select size="sm" value={chosen(item)} onChange={(e) => setFrom({ ...from, [item.id]: Number(e.target.value) })}>
                            {rows.length === 0 && <option value={0}>—</option>}
                            {rows.map((s) => (
                              <option key={s.id} value={s.id}>{s.locationName ?? s.warehouseName} · {t('stock.free')} {num(s.availableQuantity)}</option>
                            ))}
                          </Select>
                        </FormControl>
                        <FormControl w="104px">
                          <FormLabel>{t('orders.quantity')}</FormLabel>
                          <NumberInput size="sm" min={0} max={rem} value={amount(item)} onChange={(_, v) => setQty({ ...qty, [item.id]: Number.isNaN(v) ? 0 : v })}>
                            <NumberInputField />
                          </NumberInput>
                        </FormControl>
                      </HStack>
                    )}
                  </Box>
                );
              })}
              <Flex justify="flex-end" fontSize="sm" color="ink.500" gap={6}>
                <Text>HT {money(order.subTotal)}</Text><Text>TVA {money(order.taxAmount)}</Text>
                <Text color="ink.800" fontWeight={700}>{money(order.totalAmount)}</Text>
              </Flex>
            </VStack>
          )}
        </DrawerBody>
        {order && (
          <DrawerFooter borderTop="1px solid" borderColor="ink.100" justifyContent="space-between" gap={2} flexWrap="wrap">
            <HStack>
              {canCancel(order) && (
                <Button variant="ghost" colorScheme="red" isLoading={cancel.isPending} isDisabled={busy}
                  onClick={() => { if (window.confirm(t('orders.cancelConfirm'))) cancel.mutate(); }}>
                  {t('orders.cancel')}
                </Button>
              )}
              {['shipped', 'delivered'].includes(order.status.toLowerCase()) && (
                <Button variant="subtle" onClick={() => navigate(`/invoices/${order.id}`)}>{t('orders.viewInvoice')}</Button>
              )}
            </HStack>
            <HStack>
              {canReserve(order) && (
                <Button variant="subtle" isLoading={reserve.isPending} isDisabled={busy} onClick={() => reserve.mutate()}>{t('orders.reserve')}</Button>
              )}
              {canShip(order) && (
                <Button isLoading={ship.isPending} isDisabled={busy || shipment.length === 0} onClick={() => ship.mutate(shipment)}>
                  {order.status.toLowerCase() === 'partiallyshipped' ? t('orders.shipRemaining') : t('orders.ship')}
                </Button>
              )}
            </HStack>
          </DrawerFooter>
        )}
      </DrawerContent>
    </Drawer>
  );
}

/* ------------------------------ create modal ------------------------------ */

interface Line { productId: number; warehouseId: number; quantity: number }

function CreateOrderModal({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const qc = useQueryClient();
  const [customer, setCustomer] = useState({ name: '', email: '', address: '' });
  const [lines, setLines] = useState<Line[]>([{ productId: 0, warehouseId: 0, quantity: 1 }]);
  // One key per modal session: a double-click or a retry after a timeout cannot create two orders.
  const [key, setKey] = useState(() => crypto.randomUUID());

  const { data: products } = useQuery({ queryKey: ['products'], queryFn: () => productsApi.getAll().then((r) => r.data), enabled: isOpen });
  const { data: warehouses } = useQuery({ queryKey: ['warehouses'], queryFn: () => warehousesApi.getAll().then((r) => r.data), enabled: isOpen });
  const { data: stocks } = useQuery({ queryKey: ['stocks'], queryFn: () => stockApi.getAll().then((r) => r.data), enabled: isOpen });

  const free = (l: Line) =>
    (stocks ?? []).filter((s) => s.productId === l.productId && s.warehouseId === l.warehouseId).reduce((a, s) => a + s.availableQuantity, 0);
  const setLine = (i: number, patch: Partial<Line>) => setLines(lines.map((l, j) => (j === i ? { ...l, ...patch } : l)));
  const valid = customer.name.trim() !== '' && lines.length > 0 && lines.every((l) => l.productId && l.warehouseId && l.quantity > 0);

  const create = useMutation({
    mutationFn: (dto: CreateOrderDto) => ordersApi.create(dto, key).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['orders'] });
      toast({ title: t('orders.created_ok'), status: 'success' });
      setCustomer({ name: '', email: '', address: '' });
      setLines([{ productId: 0, warehouseId: 0, quantity: 1 }]);
      setKey(crypto.randomUUID());
      onClose();
    },
    onError: (e: any) => toast({ title: t('errors.error'), description: errorText(e, t('errors.generic')), status: 'error', duration: 6000 }),
  });

  return (
    <Modal isOpen={isOpen} onClose={onClose} size="2xl" scrollBehavior="inside">
      <ModalOverlay />
      <ModalContent>
        <ModalHeader>{t('orders.newOrder')}</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <VStack spacing={4} align="stretch">
            <HStack spacing={3} align="flex-start">
              <FormControl isRequired flex="1"><FormLabel>{t('orders.customerName')}</FormLabel>
                <Input value={customer.name} onChange={(e) => setCustomer({ ...customer, name: e.target.value })} /></FormControl>
              <FormControl flex="1"><FormLabel>{t('orders.customerEmail')}</FormLabel>
                <Input type="email" value={customer.email} onChange={(e) => setCustomer({ ...customer, email: e.target.value })} /></FormControl>
            </HStack>
            <FormControl><FormLabel>{t('orders.customerAddress')}</FormLabel>
              <Input value={customer.address} onChange={(e) => setCustomer({ ...customer, address: e.target.value })} /></FormControl>

            <Text fontSize="xs" fontWeight={600} color="ink.600" mt={2}>{t('orders.lines')}</Text>
            {lines.map((l, i) => {
              const max = free(l);
              return (
                <HStack key={i} spacing={2} align="flex-end">
                  <FormControl flex="2"><Select size="sm" value={l.productId} onChange={(e) => setLine(i, { productId: Number(e.target.value) })}>
                    <option value={0}>{t('orders.selectProduct')}</option>
                    {products?.map((p) => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
                  </Select></FormControl>
                  <FormControl flex="1.4"><Select size="sm" value={l.warehouseId} onChange={(e) => setLine(i, { warehouseId: Number(e.target.value) })}>
                    <option value={0}>{t('orders.selectWarehouse')}</option>
                    {warehouses?.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
                  </Select></FormControl>
                  <FormControl w="110px">
                    <NumberInput size="sm" min={1} value={l.quantity} onChange={(_, v) => setLine(i, { quantity: Number.isNaN(v) ? 0 : v })}>
                      <NumberInputField />
                    </NumberInput>
                    {l.productId > 0 && l.warehouseId > 0 && (
                      <Text mt={1} fontSize="11px" color={l.quantity > max ? 'red.500' : 'ink.400'}>{t('stock.free')}: {num(max)}</Text>
                    )}
                  </FormControl>
                  <IconButton aria-label={t('common.delete')} size="sm" variant="ghost" icon={<TrashIcon width={16} />} isDisabled={lines.length === 1}
                    onClick={() => setLines(lines.filter((_, j) => j !== i))} />
                </HStack>
              );
            })}
            <Button alignSelf="flex-start" size="sm" variant="subtle" leftIcon={<PlusIcon width={16} />}
              onClick={() => setLines([...lines, { productId: 0, warehouseId: 0, quantity: 1 }])}>{t('orders.addItem')}</Button>
          </VStack>
        </ModalBody>
        <ModalFooter gap={2}>
          <Button variant="ghost" onClick={onClose}>{t('common.cancel')}</Button>
          <Button isDisabled={!valid} isLoading={create.isPending}
            onClick={() => create.mutate({
              customerName: customer.name.trim(),
              customerEmail: customer.email || undefined,
              customerAddress: customer.address || undefined,
              taxRate: 0.2,
              items: lines.map((l) => ({ productId: l.productId, warehouseId: l.warehouseId, quantity: l.quantity })),
            })}>
            {t('orders.create')}
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

/* --------------------------------- page --------------------------------- */

const Orders = memo(() => {
  const { t } = useTranslation();
  const create = useDisclosure();
  const [openId, setOpenId] = useState<number | null>(null);
  const paged = usePaged<Order>('orders', (p) => ordersApi.getAll(p));

  return (
    <Box>
      <PageHeader
        title={t('orders.title')}
        subtitle={`${paged.total}`}
        actions={<Button leftIcon={<PlusIcon width={18} />} onClick={create.onOpen}>{t('orders.newOrder')}</Button>}
      />
      <Card>
        <Box overflowX="auto" opacity={paged.isPlaceholderData ? 0.6 : 1} transition="opacity .15s">
          <Table>
            <Thead>
              <Tr>
                <Th>{t('orders.orderNumber')}</Th><Th>{t('orders.customerName')}</Th><Th>{t('orders.orderDate')}</Th>
                <Th>{t('orders.status')}</Th><Th>{t('orders.shippedQty')}</Th><Th isNumeric>{t('orders.total')}</Th>
              </Tr>
            </Thead>
            <Tbody>
              {paged.rows.map((o) => (
                <Tr key={o.id} cursor="pointer" onClick={() => setOpenId(o.id)}>
                  <Td fontWeight={600}>{o.orderNumber}</Td>
                  <Td>{o.customerName}</Td>
                  <Td color="ink.500">{new Date(o.orderDate).toLocaleDateString()}</Td>
                  <Td><StatusBadge status={o.status} /></Td>
                  <Td><Fulfilment order={o} /></Td>
                  <Td isNumeric fontWeight={600}>{money(o.totalAmount)}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Box>
        {!paged.isLoading && paged.rows.length === 0 && <EmptyState>{t('orders.noOrders')}</EmptyState>}
        <Pagination page={paged.page} pageCount={paged.pageCount} pageSize={paged.pageSize} total={paged.total} onPage={paged.setPage} onPageSize={paged.setPageSize} />
      </Card>

      <CreateOrderModal isOpen={create.isOpen} onClose={create.onClose} />
      <OrderDrawer orderId={openId} onClose={() => setOpenId(null)} />
    </Box>
  );
});

Orders.displayName = 'Orders';
export default Orders;
