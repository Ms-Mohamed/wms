import { memo, useState, useCallback, useMemo } from 'react';
import {
  Box,
  Select,
  Grid,
  Card,
  CardBody,
  Stat,
  StatLabel,
  StatNumber,
  StatHelpText,
  useColorModeValue,
  Spinner,
  Center,
  Text,
} from '@chakra-ui/react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { analyticsApi, productsApi } from '../services/api';

const Analytics = memo(() => {
  const { t } = useTranslation();
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null);
  const bgColor = useColorModeValue('white', 'gray.800');

  const { data: products } = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.getAll().then(res => res.data),
  });

  const { data: forecast, isLoading: forecastLoading } = useQuery({
    queryKey: ['forecast', selectedProductId],
    queryFn: () => analyticsApi.predict(selectedProductId!).then(res => res.data),
    enabled: !!selectedProductId,
  });

  const { data: optimization, isLoading: optimizationLoading } = useQuery({
    queryKey: ['optimization', selectedProductId],
    queryFn: () => analyticsApi.optimize(selectedProductId!).then(res => res.data),
    enabled: !!selectedProductId,
  });

  const chartData = useMemo(() => {
    if (!forecast?.forecasts) return [];
    return forecast.forecasts.map((f) => ({
      mois: f.mois,
      prediction: f.prediction,
    }));
  }, [forecast]);

  return (
    <Box>
      <Box mb={6}>
        <h1 style={{ fontSize: '2rem', fontWeight: 'bold' }}>{t('analytics.title')}</h1>
      </Box>

      <Box mb={6}>
        <Select
          placeholder={t('analytics.selectProduct')}
          value={selectedProductId || ''}
          onChange={(e) => setSelectedProductId(e.target.value ? parseInt(e.target.value) : null)}
          maxW="400px"
        >
          {products?.map((product) => (
            <option key={product.id} value={product.id}>
              {product.code} - {product.name}
            </option>
          ))}
        </Select>
      </Box>

      {selectedProductId && (
        <Grid templateColumns="repeat(auto-fit, minmax(300px, 1fr))" gap={6} mb={6}>
          {optimizationLoading ? (
            <Center>
              <Spinner />
            </Center>
          ) : optimization ? (
            <>
              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.currentStock')}</StatLabel>
                    <StatNumber>{optimization.current_stock.toFixed(2)}</StatNumber>
                  </Stat>
                </CardBody>
              </Card>

              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.reorderPoint')}</StatLabel>
                    <StatNumber>{optimization.reorder_point.toFixed(2)}</StatNumber>
                    <StatHelpText>
                      {optimization.current_stock <= optimization.reorder_point
                        ? t('analytics.criticalStock')
                        : t('analytics.sufficientStock')}
                    </StatHelpText>
                  </Stat>
                </CardBody>
              </Card>

              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.eoq')}</StatLabel>
                    <StatNumber>{optimization.eoq.toFixed(2)}</StatNumber>
                    <StatHelpText>{t('analytics.recommendedQuantity')}</StatHelpText>
                  </Stat>
                </CardBody>
              </Card>

              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.averageDailyDemand')}</StatLabel>
                    <StatNumber>{optimization.average_daily_demand.toFixed(2)}</StatNumber>
                  </Stat>
                </CardBody>
              </Card>

              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.safetyStock')}</StatLabel>
                    <StatNumber>{optimization.safety_stock.toFixed(2)}</StatNumber>
                  </Stat>
                </CardBody>
              </Card>

              <Card bg={bgColor}>
                <CardBody>
                  <Stat>
                    <StatLabel>{t('analytics.unitCost')}</StatLabel>
                    <StatNumber>{optimization.unit_cost.toFixed(2)} €</StatNumber>
                  </Stat>
                </CardBody>
              </Card>
            </>
          ) : null}
        </Grid>
      )}

      {selectedProductId && (
        <Box bg={bgColor} p={6} borderRadius="lg" boxShadow="md" mb={6}>
          <Text fontSize="xl" fontWeight="bold" mb={4}>
            {t('analytics.forecastNext3Months')}
          </Text>
          {forecastLoading ? (
            <Center h="300px">
              <Spinner />
            </Center>
          ) : forecast?.forecasts && forecast.forecasts.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="mois" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line type="monotone" dataKey="prediction" stroke="#3182CE" name={t('analytics.forecast')} />
              </LineChart>
            </ResponsiveContainer>
          ) : (
            <Center h="300px">
              <Text>{forecast?.message || t('analytics.noData')}</Text>
            </Center>
          )}
        </Box>
      )}
    </Box>
  );
});

Analytics.displayName = 'Analytics';

export default Analytics;

