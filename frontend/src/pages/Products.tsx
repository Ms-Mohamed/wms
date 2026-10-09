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
  NumberInput,
  NumberInputField,
  HStack,
  useToast,
  IconButton,
  Select,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { productsApi, locationsApi } from '../services/api';
import type { Product } from '../types';
import { usePaged } from '../hooks/usePaged';
import Card from '../components/ui/Card';
import EmptyState from '../components/ui/EmptyState';
import PageHeader from '../components/ui/PageHeader';
import Pagination from '../components/ui/Pagination';
import { PencilIcon, TrashIcon } from '@heroicons/react/24/outline';

const Products = memo(() => {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { isOpen, onOpen, onClose } = useDisclosure();
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);

  const [formData, setFormData] = useState({
    code: '',
    name: '',
    description: '',
    unitPrice: 0,
    costPrice: 0,
    unit: 'PIECE',
    defaultLocationId: 0,
  });

  const { data: locations } = useQuery({
    queryKey: ['locations'],
    queryFn: () => locationsApi.getAll().then(res => res.data),
  });

  const paged = usePaged<Product>('products', (p) => productsApi.getPage(p));
  const products = paged.rows;

  const createMutation = useMutation({
    mutationFn: (data: Partial<Product>) => productsApi.create(data).then(res => res.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
      toast({ title: t('common.success'), status: 'success' });
      onClose();
      resetForm();
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: Partial<Product> }) =>
      productsApi.update(id, data).then(res => res.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
      toast({ title: t('common.success'), status: 'success' });
      onClose();
      resetForm();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => productsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
      toast({ title: t('common.success'), status: 'success' });
    },
    onError: (error: any) => {
      const errorMessage = error?.response?.data?.error || t('common.error');
      toast({
        title: t('common.error'),
        description: errorMessage,
        status: 'error',
        duration: 5000,
        isClosable: true,
      });
    },
  });

  const resetForm = useCallback(() => {
    setFormData({
      code: '',
      name: '',
      description: '',
      unitPrice: 0,
      costPrice: 0,
      unit: 'PIECE',
      defaultLocationId: 0,
    });
    setEditingProduct(null);
  }, []);

  const handleEdit = useCallback((product: Product) => {
    setEditingProduct(product);
    setFormData({
      code: product.code,
      name: product.name,
      description: product.description,
      unitPrice: product.unitPrice,
      costPrice: product.costPrice,
      unit: product.unit,
      defaultLocationId: product.defaultLocationId || 0,
    });
    onOpen();
  }, [onOpen]);

  const handleSubmit = useCallback(() => {
    if (editingProduct) {
      updateMutation.mutate({ id: editingProduct.id, data: formData });
    } else {
      createMutation.mutate(formData);
    }
  }, [editingProduct, formData, createMutation, updateMutation]);

  return (
    <Box>
      <PageHeader
        title={t('products.title')}
        subtitle={`${paged.total}`}
        actions={<Button onClick={() => { resetForm(); onOpen(); }}>{t('products.createProduct')}</Button>}
      />

      <Card>
        <Box overflowX="auto" opacity={paged.isPlaceholderData ? 0.6 : 1}>
        <Table>
          <Thead>
            <Tr>
              <Th>{t('products.code')}</Th>
              <Th>{t('products.name')}</Th>
              <Th isNumeric>{t('products.unitPrice')}</Th>
              <Th isNumeric>{t('products.stockQuantity')}</Th>
              <Th>{t('common.actions')}</Th>
            </Tr>
          </Thead>
          <Tbody>
            {products.map((product) => (
              <Tr key={product.id}>
                <Td fontWeight={600}>{product.code}</Td>
                <Td>{product.name}</Td>
                <Td isNumeric>{product.unitPrice.toFixed(2)} €</Td>
                <Td isNumeric>{product.stockQuantity || 0}</Td>
                <Td>
                  <HStack spacing={2}>
                    <IconButton
                      aria-label="Edit"
                      icon={<Box as={PencilIcon} w={4} h={4} />}
                      size="sm"
                      variant="ghost"
                      onClick={() => handleEdit(product)}
                    />
                    <IconButton
                      aria-label="Delete"
                      icon={<Box as={TrashIcon} w={4} h={4} />}
                      size="sm"
                      variant="ghost"
                      colorScheme="red"
                      onClick={() => {
                        if (window.confirm(t('products.confirmDelete'))) {
                          deleteMutation.mutate(product.id);
                        }
                      }}
                    />
                  </HStack>
                </Td>
              </Tr>
            ))}
          </Tbody>
        </Table>
        </Box>
        {!paged.isLoading && products.length === 0 && <EmptyState>{t('products.empty')}</EmptyState>}
        <Pagination page={paged.page} pageCount={paged.pageCount} pageSize={paged.pageSize} total={paged.total} onPage={paged.setPage} onPageSize={paged.setPageSize} />
      </Card>

      <Modal isOpen={isOpen} onClose={onClose}>
        <ModalOverlay />
        <ModalContent>
          <ModalHeader>
            {editingProduct ? t('products.editProduct') : t('products.createProduct')}
          </ModalHeader>
          <ModalCloseButton />
          <ModalBody pb={6}>
            <FormControl mb={4}>
              <FormLabel>{t('products.code')}</FormLabel>
              <Input
                value={formData.code}
                onChange={(e) => setFormData({ ...formData, code: e.target.value })}
              />
            </FormControl>

            <FormControl mb={4}>
              <FormLabel>{t('products.name')}</FormLabel>
              <Input
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              />
            </FormControl>

            <FormControl mb={4}>
              <FormLabel>{t('products.description')}</FormLabel>
              <Input
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </FormControl>

            <HStack mb={4}>
              <FormControl>
                <FormLabel>{t('products.unitPrice')}</FormLabel>
                <NumberInput
                  value={formData.unitPrice}
                  onChange={(_, val) => setFormData({ ...formData, unitPrice: val })}
                >
                  <NumberInputField />
                </NumberInput>
              </FormControl>

              <FormControl>
                <FormLabel>{t('products.costPrice')}</FormLabel>
                <NumberInput
                  value={formData.costPrice}
                  onChange={(_, val) => setFormData({ ...formData, costPrice: val })}
                >
                  <NumberInputField />
                </NumberInput>
              </FormControl>
            </HStack>

            <FormControl mb={4}>
              <FormLabel>Emplacement par défaut</FormLabel>
              <Select
                placeholder="Sélectionner un emplacement"
                value={formData.defaultLocationId || ''}
                onChange={(e) => setFormData({ ...formData, defaultLocationId: parseInt(e.target.value) || 0 })}
              >
                {locations?.map((loc) => (
                  <option key={loc.id} value={loc.id}>
                    {loc.name} {loc.zone ? `(${loc.zone})` : ''}
                  </option>
                ))}
              </Select>
            </FormControl>

            <HStack>
              <Button onClick={onClose}>{t('common.cancel')}</Button>
              <Button
                onClick={handleSubmit}
                isLoading={createMutation.isPending || updateMutation.isPending}
              >
                {t('common.save')}
              </Button>
            </HStack>
          </ModalBody>
        </ModalContent>
      </Modal>
    </Box>
  );
});

Products.displayName = 'Products';

export default Products;
