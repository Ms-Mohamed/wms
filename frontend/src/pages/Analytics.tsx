import { memo } from 'react';
import {
  Box,
  Heading,
  Text,
  SimpleGrid,
  Card,
  CardBody,
  Stat,
  StatLabel,
  StatNumber,
  StatHelpText,
  useColorModeValue,
  Spinner,
  Center,
  Flex,
  Icon,
  Button,
  Modal,
  ModalOverlay,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalCloseButton,
  Table,
  Thead,
  Tbody,
  Tr,
  Th,
  Td,
  useDisclosure,
  Badge,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  BarChart,
  Bar,
} from 'recharts';
import { useNavigate } from 'react-router-dom';
import { analyticsApi } from '../services/api';
import {
  CurrencyEuroIcon,
  CubeIcon,
  ExclamationTriangleIcon,
  ArrowTrendingUpIcon,
  ShoppingCartIcon
} from '@heroicons/react/24/outline';

const Analytics = memo(() => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const bgColor = useColorModeValue('white', 'gray.800');

  // Low Stock Modal Control
  const { isOpen, onOpen, onClose } = useDisclosure();

  // Queries
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ['analyticsStats'],
    queryFn: () => analyticsApi.getStats().then(res => res.data),
  });

  const { data: salesHistory, isLoading: historyLoading } = useQuery({
    queryKey: ['salesHistory'],
    queryFn: () => analyticsApi.getSalesHistory().then(res => res.data),
  });

  const { data: lowStockData, isLoading: lowStockLoading, refetch: refetchLowStock } = useQuery({
    queryKey: ['lowStock'],
    queryFn: () => analyticsApi.getLowStock().then(res => res.data),
    enabled: isOpen, // Only fetch when modal is open
  });

  const handleOpenLowStock = () => {
    refetchLowStock();
    onOpen();
  };

  return (
    <Box maxW="7xl" mx="auto" px={{ base: 4, md: 8 }} py={6}>
      <Box mb={8}>
        <Heading as="h1" size="xl" mb={2} color="blue.600">
          {t('analytics.title', 'Tableau de Bord Business')}
        </Heading>
        <Text color="gray.500" fontSize="lg">
          Analysez la performance de vos ventes et surveillez la santé de votre stock.
        </Text>
      </Box>

      {/* Global Stats Cards */}
      <SimpleGrid columns={{ base: 1, md: 3 }} gap={6} mb={8}>
        {statsLoading ? (
          <Center gridColumn="1 / -1" h="150px"><Spinner size="xl" /></Center>
        ) : (
          <>
            {/* Value Card */}
            <Card bg={bgColor} borderLeft="4px solid" borderColor="blue.500" shadow="lg" _hover={{ transform: 'translateY(-2px)', shadow: 'xl' }} transition="all 0.2s">
              <CardBody>
                <Stat>
                  <Flex alignItems="center" justifyContent="space-between" mb={2}>
                    <StatLabel fontSize="lg" fontWeight="medium" color="gray.600">Valeur Totale Stock</StatLabel>
                    <Box p={2} bg="blue.50" borderRadius="md">
                      <Icon as={CurrencyEuroIcon} w={6} h={6} color="blue.500" />
                    </Box>
                  </Flex>
                  <StatNumber fontSize="4xl" fontWeight="bold">
                    {stats?.total_stock_value?.toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' })}
                  </StatNumber>
                  <StatHelpText>Capital immobilisé actuel</StatHelpText>
                </Stat>
              </CardBody>
            </Card>

            {/* Products Card */}
            <Card bg={bgColor} borderLeft="4px solid" borderColor="green.500" shadow="lg" _hover={{ transform: 'translateY(-2px)', shadow: 'xl' }} transition="all 0.2s">
              <CardBody>
                <Stat>
                  <Flex alignItems="center" justifyContent="space-between" mb={2}>
                    <StatLabel fontSize="lg" fontWeight="medium" color="gray.600">Produits Actifs</StatLabel>
                    <Box p={2} bg="green.50" borderRadius="md">
                      <Icon as={CubeIcon} w={6} h={6} color="green.500" />
                    </Box>
                  </Flex>
                  <StatNumber fontSize="4xl" fontWeight="bold">{stats?.total_products || 0}</StatNumber>
                  <StatHelpText>Références cataloguées</StatHelpText>
                </Stat>
              </CardBody>
            </Card>

            {/* Alerts Card (Clickable) */}
            <Card
              as="button"
              onClick={handleOpenLowStock}
              bg={bgColor}
              borderLeft="4px solid"
              borderColor="red.500"
              shadow="lg"
              _hover={{ transform: 'translateY(-2px)', shadow: 'xl', bg: 'red.50' }}
              cursor="pointer"
              transition="all 0.2s"
              textAlign="left"
            >
              <CardBody w="100%">
                <Stat>
                  <Flex alignItems="center" justifyContent="space-between" mb={2}>
                    <StatLabel fontSize="lg" fontWeight="medium" color="gray.600">Alertes Stock</StatLabel>
                    <Box p={2} bg="red.50" borderRadius="md">
                      <Icon as={ExclamationTriangleIcon} w={6} h={6} color="red.500" />
                    </Box>
                  </Flex>
                  <StatNumber fontSize="4xl" fontWeight="bold" color="red.600">
                    {stats?.low_stock_count || 0}
                  </StatNumber>
                  <StatHelpText display="flex" alignItems="center">
                    <Text as="span" fontWeight="bold" mr={1}>Action requise</Text>
                    - Cliquez pour voir
                  </StatHelpText>
                </Stat>
              </CardBody>
            </Card>
          </>
        )}
      </SimpleGrid>

      {/* Business Graphs Section */}
      <SimpleGrid columns={{ base: 1, lg: 2 }} gap={8}>
        {/* Revenue Trend */}
        <Card bg={bgColor} shadow="md" borderRadius="xl">
          <CardBody>
            <Flex alignItems="center" mb={6}>
              <Icon as={ArrowTrendingUpIcon} w={6} h={6} color="purple.500" mr={3} />
              <Heading size="md">Évolution du Chiffre d'Affaires</Heading>
            </Flex>

            <Box h="300px">
              {!historyLoading && salesHistory ? (
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={salesHistory}>
                    <defs>
                      <linearGradient id="colorRevenue" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#8884d8" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#8884d8" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                    <XAxis dataKey="month" axisLine={false} tickLine={false} dy={10} />
                    <YAxis axisLine={false} tickLine={false} tickFormatter={(val) => `${val / 1000}k€`} />
                    <Tooltip
                      formatter={(val: number) => val.toLocaleString('fr-FR', { style: 'currency', currency: 'EUR' })}
                      contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Area type="monotone" dataKey="total_revenue" stroke="#8884d8" fillOpacity={1} fill="url(#colorRevenue)" name="CA" />
                  </AreaChart>
                </ResponsiveContainer>
              ) : (
                <Center h="100%"><Spinner /></Center>
              )}
            </Box>
          </CardBody>
        </Card>

        {/* Order Volume */}
        <Card bg={bgColor} shadow="md" borderRadius="xl">
          <CardBody>
            <Flex alignItems="center" mb={6}>
              <Icon as={CubeIcon} w={6} h={6} color="cyan.500" mr={3} />
              <Heading size="md">Volume de Commandes</Heading>
            </Flex>

            <Box h="300px">
              {!historyLoading && salesHistory ? (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={salesHistory}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E2E8F0" />
                    <XAxis dataKey="month" axisLine={false} tickLine={false} dy={10} />
                    <YAxis axisLine={false} tickLine={false} />
                    <Tooltip
                      cursor={{ fill: 'transparent' }}
                      contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Bar dataKey="total_orders" fill="#0BC5EA" radius={[4, 4, 0, 0]} name="Commandes" />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <Center h="100%"><Spinner /></Center>
              )}
            </Box>
          </CardBody>
        </Card>
      </SimpleGrid>

      {/* Low Stock Modal */}
      <Modal isOpen={isOpen} onClose={onClose} size="3xl" scrollBehavior="inside">
        <ModalOverlay backdropFilter="blur(5px)" />
        <ModalContent>
          <ModalHeader borderBottomWidth="1px" display="flex" alignItems="center" bg="red.50" color="red.700">
            <Icon as={ExclamationTriangleIcon} w={6} h={6} mr={3} />
            Produits à Réapprovisionner
          </ModalHeader>
          <ModalCloseButton />
          <ModalBody py={6}>
            {lowStockLoading ? (
              <Center py={10}><Spinner /></Center>
            ) : (
              <Table variant="simple">
                <Thead bg="gray.50">
                  <Tr>
                    <Th>Code</Th>
                    <Th>Produit</Th>
                    <Th isNumeric>Stock Actuel</Th>
                    <Th isNumeric>Point de Commande</Th>
                    <Th isNumeric>État</Th>
                    <Th>Action</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {lowStockData?.items?.map((item, index) => (
                    <Tr key={`${item.id}-${index}`} _hover={{ bg: "gray.50" }}>
                      <Td fontWeight="bold" fontSize="sm">{item.product_code}</Td>
                      <Td>{item.product_name}</Td>
                      <Td isNumeric fontWeight="bold" color="red.600">{item.current_stock}</Td>
                      <Td isNumeric>{item.reorder_point}</Td>
                      <Td isNumeric>
                        <Badge colorScheme="red" variant="subtle">CRITIQUE</Badge>
                      </Td>
                      <Td>
                        <Button
                          size="sm"
                          colorScheme="blue"
                          leftIcon={<Icon as={ShoppingCartIcon} />}
                          onClick={() => navigate('/purchase-orders', {
                            state: {
                              initialProduct: {
                                id: item.id,
                                unitCost: item.unit_cost
                              }
                            }
                          })}
                        >
                          Commander
                        </Button>
                      </Td>
                    </Tr>
                  ))}
                  {(!lowStockData?.items || lowStockData.items.length === 0) && (
                    <Tr>
                      <Td colSpan={5} textAlign="center" py={8} color="green.500">
                        <Flex direction="column" alignItems="center">
                          <Icon as={CubeIcon} w={8} h={8} mb={2} />
                          Tout va bien ! Aucun produit en rupture.
                        </Flex>
                      </Td>
                    </Tr>
                  )}
                </Tbody>
              </Table>
            )}
          </ModalBody>
        </ModalContent>
      </Modal>
    </Box>
  );
});

Analytics.displayName = 'Analytics';

export default Analytics;
